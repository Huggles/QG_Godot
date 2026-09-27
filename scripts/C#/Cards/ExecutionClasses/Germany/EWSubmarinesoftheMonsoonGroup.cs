using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using Godot;

public partial class EWSubmarinesoftheMonsoonGroup : EWCardLogic
{
    // No Targets() override: see EWBomberCommand. A choice of which Allied faction discards names
    // no country and no unit, and the VP is flat — no board state feeds it.

    /// <summary>
    /// The faction chosen by the first step, read by the second. Captured rather than re-prompted:
    /// the two events are separate steps now and the player must not be asked twice.
    /// </summary>
    private Faction _selectedFaction;

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            // Discard first, score second — this card is the one in the group whose two events run
            // in that order, and the order is preserved rather than normalised.
            new ResultStep(this, Choose.FactionFrom(() => new List<Faction> { Faction.UNITED_KINGDOM, Faction.UNITED_STATES, Faction.SOVIET },
                    faction => new ForceDiscardCardsChangeEvent(Faction, faction, 2))
                .OnChosen(chosen => _selectedFaction = (Faction)chosen.Value.Id)),

            new ResultStep(this, Choose.Fixed(() => new ScorePointsChangeEvent(new VPEntry(2, "Submarines of the Monsoon Group"), Faction)))
            .RequiringPreviousStep()
        };
    }
}
