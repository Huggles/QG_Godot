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
            Condition.Build(new Condition.IsPlayCardStep(), this),
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

                foreach (var unitId in FactionState.ForEnum(Faction).ActiveUnitIds)
                {
                    var unit = UnitState.ForId(unitId);
                    if (targetCountries.Contains(unit.CountryState))
                        unit.SuppliedForTurn = true;
                }

                PlayerActionLabel.ShowText("Truk: Japanese pieces in or adjacent to the Central Pacific are in supply this turn.", Faction);
                await Task.Delay(GameSettings.DurationLong);
                PlayerActionLabel.HideText();
                return null;
            })
            .WithGuidance("Grant supply to all Japanese pieces in or adjacent to the Central Pacific"),
        };
    }
}