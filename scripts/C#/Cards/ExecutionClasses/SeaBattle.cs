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
                List<int> armyUnits = attackState.TargetUnitIds.Where(unitId => UnitState.ForId(unitId).Type == UnitType.NAVY).ToList();
                int selectedUnitId = await new SelectUnitHandler(armyUnits).Handle();

                BattleUnitChangeEvent battleUnitChangeEvent = BuildChangeEvent(new BattleUnitChangeEvent(Faction, selectedUnitId));
                battleUnitChangeEvent.IsTrigger = true;
                CardPlayPool.DoChangeEvent(battleUnitChangeEvent);
            })
        }; 
    }

    public AttackState attackState
    {
        get
        {
            return AttackState.AttackStateForFaction(Faction);
        }
    }
    
    public List<CountryState> TargetableCountryStates
    {
        get
        {
            return GameSession.BuildableCountriesForFaction(Faction).ToCountryStates().FindAll(CountryState => CountryState.IsSea);
        }
    }    

    public override bool CanPlayCard()
    {
        return base.CanPlayCard() && attackState.TargetsOfType(UnitType.NAVY).Count > 0;
    }
}
