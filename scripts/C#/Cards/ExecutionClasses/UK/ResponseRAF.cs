using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class ResponseRAF : ResponseCardLogic
{
    protected override List<Condition> CardTriggers()
    {
        List<int> ukAndAdjacent = new List<int> { CountryState.ForEnum(Country.UnitedKingdom).Id }
            .Concat(CountryState.ForEnum(Country.UnitedKingdom).ConnectedCountryIds)
            .ToList();

        return new List<Condition> {
            Condition.Build(new Condition.IsBlockRequest(), this),
            Condition.Build(new Condition.UnitAboutToBeRemoved(Faction.UNITED_KINGDOM)
                .WithCountries(ukAndAdjacent), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async () => {
                if (ActivationTrigger is RemoveUnitChangeEvent removeEvent) {
                    removeEvent.IsBlocked = true;
                    removeEvent.UnitState.ImmuneForTurn = true;
                    PresentationServices.Notification.ShowActionText("RAF: UK piece will not be removed this turn", Faction);
                    await Task.Delay(GameSettings.DurationMedium);
                }
                return null;
            })
            .WithGuidance("Do not remove your piece in or adjacent to the United Kingdom this turn")
        };
    }
}