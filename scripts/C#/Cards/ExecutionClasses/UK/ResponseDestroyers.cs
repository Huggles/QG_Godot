using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class ResponseDestroyers : ResponseCardLogic
{
    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.IsBlockRequest(), this),
            Condition.Build(new Condition.UnitAboutToBeRemoved(
                [Faction.UNITED_KINGDOM, Faction.UNITED_STATES], UnitType.NAVY, requireInSupply: true), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async () => {
                if (ActivationTrigger is RemoveUnitChangeEvent removeEvent) {
                    removeEvent.IsBlocked = true;
                    removeEvent.UnitState.ImmuneForTurn = true;
                    PresentationServices.Notification.ShowActionText("Destroyers: Navy will not be removed this turn", Faction);
                    await Task.Delay(GameSettings.DurationMedium);
                }
                return null;
            })
            .WithGuidance("Do not remove the supplied Navy this turn")
        };
    }
}