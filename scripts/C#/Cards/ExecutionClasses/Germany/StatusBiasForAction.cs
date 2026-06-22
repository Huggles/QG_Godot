using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class StatusBiasForAction : StatusCardLogic
{    
    protected override List<Condition> CardTriggers()
    {        
        return new List<Condition> {
            Condition.Build(new Condition.FactionDeployed(Faction, DeployType.BUILD), this),
            Condition.Build(new Condition.FactionHasBattleTarget(
                Faction,
                UnitType.ARMY,
                BattleTargets
                    .Select(bt => bt.Type == TargetType.UNIT ? UnitState.ForId(bt.Id).CountryId : bt.Id)
                    .Distinct()
                    .ToList()
                ), this),

        };
    }
    
    public List<BattleTarget> BattleTargets
    {
        get
        {
            List<CountryState> neighBorCountries = CardPlayPool.GetChangeEvents<DeployUnitChangeEvent>()                
                .SelectMany(ce => CountryState.ForId(ce.CountryId).ConnectedCountryStates).Distinct().ToList();
                
            List<BattleTarget> attackableCountries = neighBorCountries                
                .Where(cs => CountryState.AttackableLand(Faction).Contains(cs))
                .Select(cs => new BattleTarget(cs.Id, TargetType.COUNTRY))
                .ToList();

            List<BattleTarget> attackableUnits = neighBorCountries                
                .SelectMany(cs => {                    
                    List<UnitState> attackableUnits = UnitState.ForIds(cs.Units.Values).Where(us => us.Tags.Has(Tag.Attackable, Faction)).ToList();
                    return attackableUnits;
                })
                .Select(us => new BattleTarget(us.Id, TargetType.UNIT))
                .ToList();
            return attackableCountries.Concat(attackableUnits).ToList();
        }
    }

    public override List<CardStep> OnActivate() 
    {
        return new List<CardStep> {
            new CardStep(this, async() => {
                ForceDiscardCardsChangeEvent discardEvent = new ForceDiscardCardsChangeEvent(Faction, Faction, 1);
                discardEvent.IsTrigger = false;
                await discardEvent.ApplyChange();

                List<PresentationItem> presentationItems = PresentationItemCard.FromCardIds(discardEvent.DiscardedCardIds, false);
                await PresentationModal.Current.ShowModal(presentationItems, "Discarded cards");
                
                BattleTarget battleTarget = await new SelectBattleTargetHandler(BattleTargets).Handle();
                BattleCountryChangeEvent battleCountryChange = BuildChangeEvent(battleTarget.ToAttackChangeEvent(Faction));
                battleCountryChange.IsTrigger = true;
                return battleCountryChange;
            }).WithGuidance("Battle a country adjacent to where you've deployed an army this turn")
        };
    }
}
