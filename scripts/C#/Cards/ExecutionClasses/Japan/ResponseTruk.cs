using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class ResponseTruk : ResponseCardLogic
{
    /// <summary>
    /// The pieces this puts back in supply: every Japanese unit in or beside the Central Pacific.
    /// Read by the step and by <see cref="Targets"/>, so the preview lights exactly who benefits.
    /// </summary>
    private List<int> SupplyTargetUnitIds
    {
        get
        {
            var centralPacific = CountryState.ForEnum(Country.CentralPacific);
            var targetCountries = centralPacific.ConnectedCountryStates.Append(centralPacific).Distinct().ToList();
            return FactionState.ForEnum(Faction).ActiveUnitIds
                .Where(uid => targetCountries.Contains(UnitState.ForId(uid).CountryState))
                .ToList();
        }
    }

    public override TargetSet Targets() => TargetSet.Units(SupplyTargetUnitIds);

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.IsFactionTurn(Faction), this),
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new RequirementStep(this, Choose.Fixed(() => {
                // Guarded INSIDE the step, not with .WithCondition. A step condition would make this
                // the card's only executable step when it fails, HasExecutableCardSteps false, and
                // the card unactivatable -- where before it simply announced itself and granted
                // nothing. Same trap as the EW cards above.
                List<int> unitIds = SupplyTargetUnitIds;
                return unitIds.Count == 0 ? null : new GrantSupplyChangeEvent(Faction, unitIds);
            }))
            .WithGuidance("Grant supply to all Japanese pieces in or adjacent to the Central Pacific"),

            // Announcement only — no ChangeEvent, so an EffectStep. Deliberately NOT gated on the
            // step above: the card tells the table what it did whether or not anything needed the
            // supply, exactly as before.
            new EffectStep(this, async () => {
                PresentationServices.Notification.ShowActionText("Truk: Japanese pieces in or adjacent to the Central Pacific are in supply this turn.", Faction);
                await Task.Delay(GameSettings.DurationLong);
                PresentationServices.Notification.HideActionText();
                return CardStepResult.Nothing;
            })
        };
    }
}
