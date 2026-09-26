using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Watches the board for the conditions that award an achievement. Autoloaded, so it outlives every
/// scene change and never has to re-subscribe.
///
/// Runs on every peer: a deploy replays through <c>GameAPI.DeployUnitToCountry</c> on clients too, and
/// each peer awards only the seats its own player holds.
/// </summary>
public partial class AchievementTracker : Node
{
	public override void _Ready()
	{
		if (GameContext.IsHeadless) return;

		// SteamworksApi knows no game concepts; this is where the catalog reaches it. Safe this late:
		// it is only read when Steam delivers the player's stats, well after every autoload is up.
		SteamworksApi.Catalog = Achievements.All;
		EventBus.Instance.UnitDeployed += OnUnitDeployed;
	}

	public override void _ExitTree()
	{
		if (GameContext.IsHeadless) return;
		EventBus.Instance.UnitDeployed -= OnUnitDeployed;
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
}
