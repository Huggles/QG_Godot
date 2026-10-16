using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// The rules an Air Force brings to battles and removals, wherever it comes from (the Total War
/// expansion, rulebook pages 6-7). Host-only, like the rest of the rules loop: the choices are prompts
/// to the players concerned, and the outcomes reach every peer as ordinary replicated events.
///
/// <list type="bullet">
/// <item><b>Air Defense.</b> When an Army or Navy would be removed by a battle, its owner may remove
/// its own Air Force in the same space instead. Not for an elimination: only a battle.</item>
/// <item><b>Air Attack.</b> When the defender does, the attacker may remove one of its own supplied
/// Air Forces adjacent to the battled space to remove the original target anyway.</item>
/// <item><b>Reposition.</b> An Air Force left without its own Army or Navy moves at once to an
/// adjacent space with one of its faction's supplied Armies or Navies and no Air Force of its own,
/// or is removed if there is none.</item>
/// </list>
///
/// The removals and moves these make are bookkeeping of a reaction rather than triggers: applied with
/// IsTrigger and RegisterInPool off, like the pool-shortfall recall, so no card written for Armies
/// and Navies can react to them or read them back.
/// </summary>
public static class AirForces
{
    /// <summary>
    /// Offer Air Defense, and then Air Attack, on <paramref name="changeEvent"/> if it is a battle on a
    /// unit standing with its faction's Air Force. Called by the reaction pipeline after the battle's
    /// block window and before it applies, so a card that saves the unit outright goes first.
    /// True when an Air Force took the hit and the battle stays blocked.
    /// </summary>
    public static async Task<bool> ResolveAirDefense(ChangeEvent changeEvent)
    {
        if (changeEvent is not BattleUnitChangeEvent battle || battle.IsBlocked) return false;

        BoardState board = BoardState.Live;
        UnitState target = battle.UnitState;
        CountryState country = CountryState.ForId(battle.CountryId);
        int defendingAirForce = board.AirForceIn(target.Faction, country);
        if (defendingAirForce < 0) return false;

        Faction attacker = battle.TriggeringFaction;
        string piece = target.IsNavy ? "Navy" : "Army";

        bool defend = await Ask(target.Faction,
            $"{attacker.Label()} battles {target.Faction.Label()}'s {piece} in {country.Label}",
            "Remove the Air Force instead", $"Remove the {piece}",
            YesCard("Air Defense", $"Remove your Air Force in {country.Label} instead of your {piece}.", defendingAirForce),
            battle, TriggerKind.BLOCK);
        if (!defend) return false;

        await Remove(target.Faction, defendingAirForce, UnitRemovalReason.AIR_DEFENSE);
        battle.IsBlocked = true;

        List<int> attackingAirForces = board.SuppliedAirForcesAdjacentTo(attacker, country);
        if (attackingAirForces.Count == 0) return true;

        bool attack = await Ask(attacker,
            $"{target.Faction.Label()}'s Air Force took the hit for its {piece} in {country.Label}",
            $"Spend an Air Force to remove the {piece} anyway", "Let it stand",
            YesCard("Air Attack", $"Remove one of your supplied Air Forces next to {country.Label} to remove the {piece} anyway.",
                attackingAirForces.Append(target.Id).ToArray()),
            battle, TriggerKind.NONE);
        if (!attack) return true;

        int spent = attackingAirForces[0];
        if (attackingAirForces.Count > 1)
        {
            try
            {
                spent = (await new InputRequest.SelectUnitRequestHandler(attacker, attackingAirForces, allowSkip: false)
                    .BroadCast()).ResponseUnitIds[0];
            }
            catch (StepSkippedException)
            {
                // Timed out or released: the attack was chosen, so it goes ahead with the first one.
            }
        }

        await Remove(attacker, spent, UnitRemovalReason.AIR_ATTACK);
        battle.IsBlocked = false;
        return false;
    }

    /// <summary>
    /// If <paramref name="faction"/>'s Army or Navy has just left <paramref name="countryId"/>, move its
    /// Air Force there to an adjacent space it may stand in, asking when there is a choice, or remove
    /// it when there is none. Run by RemoveUnitChangeEvent once a ground unit's removal has applied.
    /// </summary>
    public static async Task RepositionStranded(Faction faction, int countryId)
    {
        BoardState board = BoardState.Live;
        CountryState country = CountryState.ForId(countryId);
        int airForce = board.AirForceIn(faction, country);
        if (airForce < 0 || board.HasUnit(faction, country)) return;

        List<int> targets = board.RepositionTargets(UnitState.ForId(airForce));
        if (targets.Count == 0)
        {
            await Remove(faction, airForce, UnitRemovalReason.REPOSITION_LOST);
            return;
        }

        int destination = targets[0];
        if (targets.Count > 1)
        {
            await new ShowActionLabelPresentationEvent(faction,
                $"{faction.WithPlayer()}'s Air Force in {country.Label} must reposition to an adjacent space").Apply();
            try
            {
                destination = (await new InputRequest.SelectCountryRequestHandler(faction, targets) { MarkRebuildTargets = false }
                    .BroadCast()).ResponseCountryIds[0];
            }
            catch (StepSkippedException)
            {
                // Repositioning is not optional: a skipped or timed-out choice takes the first space.
            }
        }

        await new MoveAirForceChangeEvent(faction, airForce, destination, AirForceMoveReason.REPOSITION)
        {
            IsTrigger = false,
            RegisterInPool = false,
        }.Apply();
    }

    /// <summary>
    /// A yes/no prompt to <paramref name="faction"/>'s player: yes is <paramref name="yesCard"/> in the hand fan, no
    /// the Skip button, with <paramref name="battle"/> shown as a reaction window shows its trigger. A skip or time-out answers no.
    /// </summary>
    private static async Task<bool> Ask(Faction faction, string title, string yes, string no, InputRequest.OptionCard yesCard,
        ChangeEvent battle, TriggerKind kind)
    {
        try
        {
            InputRequest request = new InputRequest.SelectOptionRequestHandler(faction, new List<string> { yes, no }, title)
            {
                OptionCards = new List<InputRequest.OptionCard> { yesCard },
                TriggerReactionKind = kind,
            };
            request.StampTrigger(battle, battle.SourceCardId);
            InputRequest answer = await request.BroadCast();
            return answer.ResponseCardIds.Count > 0 && answer.ResponseCardIds[0] == 0;
        }
        catch (StepSkippedException)
        {
            return false;
        }
    }

    /// <summary>The yes of <see cref="Ask"/> (option 0) as a card, lighting the pieces it would remove.</summary>
    private static InputRequest.OptionCard YesCard(string title, string text, params int[] unitIds) => new()
    {
        OptionId = 0, Title = title, Text = text, ArtCardType = CardType.AIR_POWER, TargetUnitIds = unitIds.ToList(),
    };

    private static Task Remove(Faction faction, int airForceId, UnitRemovalReason reason) =>
        new RemoveUnitChangeEvent(faction, airForceId, reason)
        {
            IsTrigger = false,
            RegisterInPool = false,
        }.Apply();
}
