using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class ResponseLeningrad : ResponseCardLogic
{

    List<int> targetCountries = [(int)Country.Russia];
    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.IsBlockRequest(), this),
            Condition.Build(new Condition.CustomCondition(()=>{
                if(CardPlayPool.LastNoneNewCardChangeEvent is BattleUnitChangeEvent battleUnitChangeEvent){
                    return battleUnitChangeEvent.UnitState.Faction == Faction.SOVIET && targetCountries.Contains(battleUnitChangeEvent.CountryId);
                }
                return false;
            }), this),

        };
    }
    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new CardStep(this, async ()=>{
                if(CardPlayPool.LastNoneNewCardChangeEvent is BattleUnitChangeEvent battleUnitChangeEvent){
                    battleUnitChangeEvent.IsBlocked = true;
                    battleUnitChangeEvent.UnitState.ImmuneForTurn = true;
                    PresentationServices.Notification.ShowActionText($"{FactionState.ForEnum(Faction).FactionData.Label} prevented the land battle on his army in {CountryState.ForId(targetCountries[0])}", Faction);
                    await Task.Delay(GameSettings.DurationMedium);
                    return null;
                }else {
                    throw new Exception("Reaction should be to a discard change event");
                }
            })
        };
    }
}