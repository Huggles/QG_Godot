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

    private CardStep MakeFactionStep(Faction targetFaction)
    {
        return new CardStep(this, async () => {
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
                RemoveUnitChangeEvent removeEvent = BuildChangeEvent(
                    new RemoveUnitChangeEvent(Faction, selectedUnitId, UnitRemovalReason.ELIMINATE));
                removeEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(removeEvent);
            }
            else
            {
                ForceDiscardCardsChangeEvent discardEvent = BuildChangeEvent(
                    new ForceDiscardCardsChangeEvent(Faction, targetFaction, 2));
                discardEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(discardEvent);
            }
        })
        .WithGuidance($"{targetFaction}: discard 2 cards or eliminate a Mediterranean Navy");
    }
}
