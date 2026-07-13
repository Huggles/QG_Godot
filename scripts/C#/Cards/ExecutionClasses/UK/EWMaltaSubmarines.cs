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

            List<PresentationItem> options = new() {
                new PresentationItemImageButton(CHOICE_DISCARD,   "Discard top 2 cards from draw deck",        true),
                new PresentationItemImageButton(CHOICE_ELIMINATE, "Eliminate a Navy in the Mediterranean", navies.Count > 0)
            };

            Variant[] response = await PresentationModal.Current.ShowModal(
                options, $"{targetFaction} must choose a penalty", requireSelection: true);
            int choice = response[0].As<int>();
            await PresentationModal.Current.HideModal();

            if (choice == CHOICE_ELIMINATE && navies.Count > 0)
            {
                int selectedUnitId = (await new InputRequest.SelectUnitRequestHandler(Faction, navies).BroadCast()).ResponseUnitIds[0];
                RemoveUnitChangeEvent removeEvent = BuildChangeEvent(
                    new RemoveUnitChangeEvent(Faction, selectedUnitId, UnitRemovalReason.ELIMINATE));
                removeEvent.IsTrigger = true;
                return removeEvent;
            }
            else
            {
                ForceDiscardCardsChangeEvent discardEvent = BuildChangeEvent(
                    new ForceDiscardCardsChangeEvent(Faction, targetFaction, 2));
                discardEvent.IsTrigger = true;
                return discardEvent;
            }
        })
        .WithGuidance($"{targetFaction}: discard 2 cards or eliminate a Mediterranean Navy");
    }
}
