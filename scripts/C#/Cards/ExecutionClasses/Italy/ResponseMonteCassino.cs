using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class ResponseMonteCassino : ResponseCardLogic
{
    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.IsBlockRequest(), this),
            Condition.Build(new Condition.CustomCondition(() => {
                if (CardPlayPool.CurrentBlockTrigger is not RemoveUnitChangeEvent removeEvent) return false;
                return StaticGameData.FactionTeamForFaction(removeEvent.UnitState.Faction) == FactionTeam.AXIS
                    && removeEvent.UnitState.Type == UnitType.ARMY
                    && removeEvent.CountryId == (int)Country.Italy;
            }), this),
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async () => {
                if (ActivationTrigger is RemoveUnitChangeEvent removeEvent) {
                    removeEvent.IsBlocked = true;
                    removeEvent.UnitState.ImmuneForTurn = true;
                    PresentationServices.Notification.ShowActionText("Monte Cassino: Axis Army in Italy will not be removed this turn", Faction);
                    await Task.Delay(GameSettings.DurationMedium);
                }
                return null;
            })
            .WithGuidance("Prevent the removal of an Axis Army in Italy this turn")
        };
    }
}