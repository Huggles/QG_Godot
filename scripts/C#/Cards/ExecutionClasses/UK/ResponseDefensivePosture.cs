using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class ResponseDefensivePosture : ResponseCardLogic
{
    /// <summary>The supplied UK Army about to be removed. The block window has already named it, so this card picks nothing.</summary>
    public override TargetSet Targets() => BlockTargets();

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.IsBlockRequest(), this),
            Condition.Build(new Condition.UnitAboutToBeRemoved(Faction.UNITED_KINGDOM, UnitType.ARMY, requireInSupply: true), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async () => {
                if (ActivationTrigger is RemoveUnitChangeEvent removeEvent) {
                    removeEvent.IsBlocked = true;
                    removeEvent.UnitState.ImmuneForTurn = true;
                    PresentationServices.Notification.ShowActionText("Defensive Posture: UK Army will not be removed this turn", Faction);
                    await Task.Delay(GameSettings.DurationMedium);
                }
            })
            .WithGuidance("Do not remove your supplied Army this turn")
        };
    }
}
