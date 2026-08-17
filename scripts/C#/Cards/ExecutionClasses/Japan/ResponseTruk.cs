using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class ResponseTruk : ResponseCardLogic
{
    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.IsFactionTurn(Faction), this),
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async () => {
                var centralPacific = CountryState.ForEnum(Country.CentralPacific);
                var targetCountries = centralPacific.ConnectedCountryStates
                    .Append(centralPacific)
                    .Distinct()
                    .ToList();

                var unitIds = FactionState.ForEnum(Faction).ActiveUnitIds
                    .Where(uid => targetCountries.Contains(UnitState.ForId(uid).CountryState))
                    .ToList();

                if (unitIds.Count > 0)
                {
                    GrantSupplyChangeEvent grantEvent = BuildChangeEvent(new GrantSupplyChangeEvent(Faction, unitIds));
                    grantEvent.IsTrigger = false;
                    await grantEvent.Apply();
                }

                PresentationServices.Notification.ShowActionText("Truk: Japanese pieces in or adjacent to the Central Pacific are in supply this turn.", Faction);
                await Task.Delay(GameSettings.DurationLong);
                PresentationServices.Notification.HideActionText();
            })
            .WithGuidance("Grant supply to all Japanese pieces in or adjacent to the Central Pacific"),
        };
    }
}