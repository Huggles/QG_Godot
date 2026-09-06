using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class ResponseBattleshipRepair : ResponseCardLogic
{
    /// <summary>The supplied Japanese Navy about to be removed. The block window has already named it, so this card picks nothing.</summary>
    public override TargetSet Targets() => BlockTargets();

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.IsBlockRequest(), this),
            Condition.Build(new Condition.UnitAboutToBeRemoved(Faction, UnitType.NAVY, requireInSupply: true), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async () => {
                if (ActivationTrigger is RemoveUnitChangeEvent removeEvent) {
                    removeEvent.IsBlocked = true;
                    removeEvent.UnitState.ImmuneForTurn = true;
                    PresentationServices.Notification.ShowActionText("Battleship Repair: Japanese Navy will not be removed this turn", Faction);
                    await Task.Delay(GameSettings.DurationMedium);
                }
            })
            .WithGuidance("Do not remove your supplied Navy this turn")
        };
    }
}