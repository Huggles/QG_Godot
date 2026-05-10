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
                CardPlayPool.GetChangeEvents<DeployUnitChangeEvent>().Map(changeEvent => changeEvent.CountryId)
                ), this),

        };
    }
    
    public List<BattleTarget> BattleTargets
    {
        get
        {
            List<BattleTarget> battleTargets = CardPlayPool.GetChangeEvents<DeployUnitChangeEvent>()
                .Map(changeEvent => changeEvent.CountryId)
                .SelectMany(countryId => CountryState.ForId(countryId).AdjacentBattleTargets(Faction, CountryType.LAND)).Distinct().ToList();
            return battleTargets;
        }
    }

    public override List<CardStep> InitializeReactCardSteps() 
    {
        return new List<CardStep> {
            new CardStep(this, async() => {
                List<PresentationItem> presentationItems = PresentationItemCard.FromCardIds(DeckState.ForFaction(Faction).DiscardTopCards(1), false);
                await PresentationModal.Instance.ShowModal(presentationItems, "Discarded cards");
                
                BattleTarget battleTarget = await new SelectBattleTargetHandler(BattleTargets).Handle();
                BattleCountryChangeEvent battleCountryChange = BuildChangeEvent(battleTarget.ToAttackChangeEvent(Faction));
                battleCountryChange.IsTrigger = true;
                return battleCountryChange;
            }).WithGuidance("Battle a country adjacent to where you've deployed an army this turn")
        };
    }
}
