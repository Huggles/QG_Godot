using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

/// <summary>
/// Watches the board for the conditions that award an achievement. Autoloaded, so it outlives every
/// scene change and never has to re-subscribe.
///
/// Runs on every peer: a deploy replays through <c>GameAPI.DeployUnitToCountry</c> on clients too, and
/// each peer awards only the seats its own player holds.
/// </summary>
public partial class AchievementTracker : Node
{
	// The range of the Axis lead at the end of each round so far, for Against All Odds.
	private int _lowestAxisLead, _highestAxisLead;

	public override void _Ready()
	{
		if (GameContext.IsHeadless) return;

		// SteamworksApi knows no game concepts; this is where the catalog reaches it. Safe this late:
		// it is only read when Steam delivers the player's stats, well after every autoload is up.
		SteamworksApi.Catalog = Achievements.All;
		EventBus.Instance.UnitDeployed += OnUnitDeployed;
		EventBus.Instance.GameChangeEventAfter += OnGameMessageApplied;
		EventBus.Instance.GameEnded += OnGameEnded;
	}

	public override void _ExitTree()
	{
		if (GameContext.IsHeadless) return;
		EventBus.Instance.UnitDeployed -= OnUnitDeployed;
		EventBus.Instance.GameChangeEventAfter -= OnGameMessageApplied;
		EventBus.Instance.GameEnded -= OnGameEnded;
	}

	/// <summary>
	/// Nothing here may throw: EventBus runs a signal's handlers through one multicast delegate, so an
	/// escaping exception would abort the emission and skip every subscriber behind this one.
	/// </summary>
	private void OnUnitDeployed(int unitId, int countryId)
	{
		if (!SteamworksApi.IsAvailable) return;

		try
		{
			UnitState unit = UnitState.ForId(unitId);
			if (unit == null || !unit.IsArmy) return;

			// The human's own seats only: a host answering for five bots has not earned what they do.
			List<Faction> mine = PlayerFactionRegistry.GetLocalHumanFactions();
			if (!mine.Contains(unit.Faction)) return;

			SteamworksApi.Unlock(Achievements.ArmyIn(unit.Faction, (Country)countryId));
		}
		catch (Exception e)
		{
			DebugUtilities.PrintPeerError($"Achievement check failed for unit {unitId}: {e.Message}");
		}
	}

	/// <summary>Emitted right after a message joins the journal, so the journal's last entry is the one just applied.</summary>
	private void OnGameMessageApplied(string messageName)
	{
		if (!SteamworksApi.IsAvailable) return;

		try
		{
			List<GameMessage> journal = MultiplayerSession.Instance?.GameState?.GameMessages;
			switch (journal is { Count: > 0 } ? journal[^1] : null)
			{
				case RemoveUnitChangeEvent { Reason: UnitRemovalReason.SUPPLY } loss: OnSupplyLoss(loss, journal);         break;
				case RemoveUnitChangeEvent removal:                                   OnUnitRemoved(removal, journal);    break;
				case ChangeRoundChangeEvent turn:                                     OnTurnStarted(turn);                break;
				case ChangeEvent discard when EnemyDiscards(discard) > 0:             OnEnemyDiscarded(discard, journal); break;
			}
		}
		catch (Exception e)
		{
			DebugUtilities.PrintPeerError($"Achievement check failed for {messageName}: {e.Message}");
		}
	}

	private static void OnUnitRemoved(RemoveUnitChangeEvent removal, List<GameMessage> journal)
	{
		// A restored save replays kills that were already counted when they happened.
		if (ReplayContext.IsFastForwarding || !IsElimination(removal)) return;

		List<Faction> mine = PlayerFactionRegistry.GetLocalHumanFactions();
		if (LocalTeam(mine) == null || !mine.Contains(StaticGameData.ControllerOf(removal.TriggeringFaction))) return;

		SteamworksApi.AddProgress(Achievements.WarOfAttrition);

		if (journal.OfType<RemoveUnitChangeEvent>().First(IsElimination) != removal) return;
		SteamworksApi.Unlock(Achievements.FirstBlood);
		if (removal.TriggeringFaction == Faction.UNITED_STATES) SteamworksApi.Unlock(Achievements.FirstBloodUs);
	}

	/// <summary>
	/// No faction causes a supply loss, so every player on the other team shares it. A faction only loses
	/// units to supply in its own turn, so counting back to the turn's start counts one enemy's losses.
	/// </summary>
	private static void OnSupplyLoss(RemoveUnitChangeEvent loss, List<GameMessage> journal)
	{
		FactionTeam? enemy = LocalTeam(PlayerFactionRegistry.GetLocalHumanFactions()) is { } team ? StaticGameData.OpponentTeam(team) : null;
		if (enemy == null || loss.UnitState?.FactionTeam != enemy) return;

		int thisTurn = 0;
		for (int i = journal.Count - 1; i >= 0 && journal[i] is not ChangeRoundChangeEvent; i--)
			if (journal[i] is RemoveUnitChangeEvent { Reason: UnitRemovalReason.SUPPLY } earlier && earlier.UnitState?.FactionTeam == enemy)
				thisTurn++;

		foreach (SteamAchievement achievement in Achievements.SupplyLosses.Take(thisTurn))
			SteamworksApi.Unlock(achievement);
	}

	/// <summary>A unit removed by the other side: a battle or an event, never a supply loss or a recall.</summary>
	private static bool IsElimination(RemoveUnitChangeEvent removal)
		=> removal.UnitState is { } unit && unit.FactionTeam == StaticGameData.OpponentFactionTeamForFaction(removal.TriggeringFaction);

	/// <summary>Not skipped while a save fast-forwards: unlocking twice is harmless, and the round's earlier discards are in the journal either way.</summary>
	private static void OnEnemyDiscarded(ChangeEvent discard, List<GameMessage> journal)
	{
		Faction attacker = discard.TriggeringFaction;
		List<Faction> mine = PlayerFactionRegistry.GetLocalHumanFactions();
		if (LocalTeam(mine) == null || !mine.Contains(attacker)) return;

		int thisGame = journal.OfType<ChangeEvent>().Where(e => e.TriggeringFaction == attacker).Sum(EnemyDiscards);
		if (thisGame >= Achievements.EconomicStrangleholdCards) SteamworksApi.Unlock(Achievements.EconomicStranglehold);

		if (Achievements.DiscardGoalFor(attacker) is not { } goal) return;
		int thisRound = 0;
		for (int i = journal.Count - 1; i >= 0 && !StartsRound(journal[i]); i--)
			if (journal[i] is ChangeEvent earlier && earlier.TriggeringFaction == attacker && earlier.TargetFaction == discard.TargetFaction)
				thisRound += EnemyDiscards(earlier);

		if (thisRound >= goal.Cards) SteamworksApi.Unlock(goal.Achievement);
	}

	/// <summary>Cards a card forced a faction of the other team to discard, from its deck or its hand. Voluntary discards and a card's own costs never count.</summary>
	private static int EnemyDiscards(ChangeEvent change)
	{
		if (StaticGameData.FactionTeamForFaction(change.TargetFaction) != StaticGameData.OpponentFactionTeamForFaction(change.TriggeringFaction)) return 0;
		return change switch
		{
			ForceDiscardCardsChangeEvent deck     => deck.DiscardedCardIds.Count,
			ForceDiscardHandCardsChangeEvent hand => hand.DiscardedCardIds.Count,
			_                                     => 0,
		};
	}

	private static bool StartsRound(GameMessage message)
		=> message is ChangeRoundChangeEvent turn && StaticGameData.RoundForTurn(turn.NewTurn) != StaticGameData.RoundForTurn(turn.NewTurn - 1);

	/// <summary>Samples the score gap as each round ends. Runs through a save's replay too, which rebuilds the same history.</summary>
	private void OnTurnStarted(ChangeRoundChangeEvent turn)
	{
		if (turn.NewTurn == 1) ResetLead();
		if (!StartsRound(turn)) return;

		int axisLead = StaticGameData.ScoreForTeam(FactionTeam.AXIS) - StaticGameData.ScoreForTeam(FactionTeam.ALLIES);
		_lowestAxisLead  = Math.Min(_lowestAxisLead, axisLead);
		_highestAxisLead = Math.Max(_highestAxisLead, axisLead);
	}

	private void OnGameEnded(string resultJson)
	{
		if (!SteamworksApi.IsAvailable) return;

		try
		{
			GameResult result = JsonSerializer.Deserialize<GameResult>(resultJson);

			// A tutorial's game is scripted, so its win is not the player's. Host-only, as tutorials are.
			if (result.IsDraw || GameFlow.Instance?.Program != null) return;
			List<Faction> mine = PlayerFactionRegistry.GetLocalHumanFactions();
			if (LocalTeam(mine) is not { } team || result.WinningTeam != team) return;

			SteamworksApi.Unlock(Achievements.FirstVictory);
			foreach (Faction faction in mine) SteamworksApi.Unlock(Achievements.VictoryAs(faction));
			SteamworksApi.AddProgress(Achievements.VeteranCommander, announce: true);

			if (Math.Abs(result.AxisTotal - result.AlliesTotal) >= GameFlow.DecisiveLead)
			{
				SteamworksApi.Unlock(Achievements.DecisiveVictory);
				if (result.FinalRound <= Achievements.LightningWarLastRound) SteamworksApi.Unlock(Achievements.LightningWar);
			}

			int worstDeficit = team == FactionTeam.AXIS ? -_lowestAxisLead : _highestAxisLead;
			if (worstDeficit >= Achievements.AgainstAllOddsDeficit) SteamworksApi.Unlock(Achievements.AgainstAllOdds);
		}
		catch (Exception e)
		{
			DebugUtilities.PrintPeerError($"Achievement check failed at game end: {e.Message}");
		}
		finally
		{
			ResetLead();
		}
	}

	private void ResetLead() => _lowestAxisLead = _highestAxisLead = 0;

	/// <summary>The one team the local player's seats are on, or null when they hold none or play both sides.</summary>
	private static FactionTeam? LocalTeam(List<Faction> mine)
	{
		List<FactionTeam> teams = mine.Select(StaticGameData.FactionTeamForFaction).Distinct().ToList();
		return teams.Count == 1 ? teams[0] : null;
	}
}
