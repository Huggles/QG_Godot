using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class SeaBattle : CardLogic
{
    public override List<CardStep> InitializePlayCardSteps()
    {
        return new List<CardStep> {
            new CardStep(this, async() => {
                GameStateCalculator calculator = GameStateCalculator.CalculateAllForFaction(Faction);
                List<int> armyUnits = calculator.TargetUnitIds.Where(unitId => UnitState.ForId(unitId).Type == UnitType.NAVY).ToList();
                int selectedUnitId = await new SelectUnitHandler(armyUnits).Handle();

                BattleUnitChangeEvent battleUnitChangeEvent = BuildChangeEvent(new BattleUnitChangeEvent(Faction, selectedUnitId));
                battleUnitChangeEvent.IsTrigger = true;
                return battleUnitChangeEvent;
            })
            .WithCondition(()=>Condition.Build(new Condition.HasSeaBattleTarget(Faction), this))            
        }; 
    }
}
