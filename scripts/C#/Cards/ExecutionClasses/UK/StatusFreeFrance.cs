using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class StatusFreeFrance : StatusCardLogic
{
    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.IsPlayCardStep(), this),
            Condition.Build(new Condition.CountryIsBuildable([(int)Country.WesternEurope], Faction), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async () => {
                ForceDiscardCardsChangeEvent discardEvent = BuildChangeEvent(new ForceDiscardCardsChangeEvent(Faction, Faction, 2));
                discardEvent.IsTrigger = false;
                await discardEvent.ApplyChange();

                List<PresentationItem> presentationItems = PresentationItemCard.FromCardIds(discardEvent.DiscardedCardIds, false);
                await PresentationModal.Current.ShowModal(presentationItems, "Discarded cards");

                DeployUnitChangeEvent deployEvent = BuildChangeEvent(
                    new DeployUnitChangeEvent(Faction, (int)Country.WesternEurope, DeployType.BUILD));
                deployEvent.IsTrigger = true;
                return deployEvent;
            })
            .WithCondition(() => Condition.Build(new Condition.CountryIsBuildable([(int)Country.WesternEurope], Faction), this))
            .WithGuidance("Discard top 2 deck cards to build an Army in Western Europe")
        };
    }
}