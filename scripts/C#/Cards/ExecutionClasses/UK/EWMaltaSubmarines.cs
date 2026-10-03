using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class EWMaltaSubmarines : EWCardLogic
{
    private const int CHOICE_DISCARD   = 0;
    private const int CHOICE_ELIMINATE = 1;

    private List<int> MediterraneanNaviesFor(Faction faction) =>
        CountryState.ForEnum(Country.MediterraneanSea).Units.Values
            .Where(uId => UnitState.ForId(uId).Faction == faction
                       && UnitState.ForId(uId).IsNavy
                       && !UnitState.ForId(uId).ImmuneForTurn)
            .ToList();

    /// <summary>The Axis navies in the Mediterranean that may be sunk instead of discarding. The
    /// choice is the target's, not this card's, so both factions' navies are reported — the
    /// Mediterranean itself too, since a discard-only outcome still happens there.</summary>
    public override TargetSet Targets() =>
        TargetSet.Countries(new List<Country> { Country.MediterraneanSea })
            .Plus(TargetSet.Units(MediterraneanNaviesFor(Faction.GERMANY)))
            .Plus(TargetSet.Units(MediterraneanNaviesFor(Faction.ITALY)));

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            MakeFactionStep(Faction.GERMANY),
            MakeFactionStep(Faction.ITALY)
        };
    }

    /// <summary>
    /// One step per Axis faction. NOT split: statically there are two possible event types here, but
    /// at runtime the target picks one, so exactly one event is produced -- which is the case that
    /// justifies a step RETURNING its event rather than declaring it up front.
    /// </summary>
    private CardStep MakeFactionStep(Faction targetFaction)
    {
        return new ResultStep(this, async () => {
            List<int> navies = MediterraneanNaviesFor(targetFaction);

            List<string> penaltyLabels = new() { "Discard top 2 cards from draw deck" };
            List<int> penaltyIds = new() { CHOICE_DISCARD };
            if (navies.Count > 0)
            {
                penaltyLabels.Add("Eliminate a Navy in the Mediterranean");
                penaltyIds.Add(CHOICE_ELIMINATE);
            }

            var penaltyResp = await new InputRequest.SelectOptionRequestHandler(
                targetFaction, penaltyLabels, penaltyIds,
                $"{targetFaction} must choose a penalty").BroadCast();
            int choice = penaltyResp.ResponseCardIds[0];

            if (choice == CHOICE_ELIMINATE && navies.Count > 0)
            {
                int selectedUnitId = (await new InputRequest.SelectUnitRequestHandler(targetFaction, navies).BroadCast()).ResponseUnitIds[0];
                return new RemoveUnitChangeEvent(Faction, selectedUnitId, UnitRemovalReason.ELIMINATE);
            }

            return new ForceDiscardCardsChangeEvent(Faction, targetFaction, 2);
        })
        .WithGuidance($"Make {targetFaction.Label()} discard 2 cards or eliminate a Mediterranean Navy");
    }
}
