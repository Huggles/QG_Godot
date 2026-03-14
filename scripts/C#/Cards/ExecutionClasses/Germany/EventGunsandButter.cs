using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class EventGunsAndButter : EventCardLogic
{
    public override List<CardStep> InitializePlayCardSteps()
    {
        return new List<CardStep> {
            new CardStep(this, async() => {
                // Show modal to choose which action to take
                List<PresentationItem> actionOptions = new List<PresentationItem>();
                
                if (CountryState.BuildableLand(Faction).Any())
                    actionOptions.Add(new PresentationItemImageButton(1, "Build an army", true));
                
                if (CountryState.BuildableSea(Faction).Any())
                    actionOptions.Add(new PresentationItemImageButton(2, "Build a navy", true));
                
                if (UnitState.AttackableArmies(Faction).Any() || CountryState.AttackableLand(Faction).Any())
                    actionOptions.Add(new PresentationItemImageButton(3, "Land battle", true));
                
                if (UnitState.AttackableNavies(Faction).Any() || CountryState.AttackableSea(Faction).Any())
                    actionOptions.Add(new PresentationItemImageButton(4, "Sea battle", true));
                
                Variant[] response = await PresentationModal.Instance.ShowModal(actionOptions, "Choose an action");
                int selectedAction = response[0].As<int>();
                await PresentationModal.Instance.HideModal();
                
                // Execute the chosen action
                ChangeEvent result = null;
                switch (selectedAction)
                {
                    case 1: // Build Army
                        var buildableLand = CountryState.BuildableLand(Faction);
                        int armyCountryId = await new SelectCountryHandler(buildableLand.ToCountryIds()).Handle();
                        result = BuildChangeEvent(new DeployUnitChangeEvent(Faction, armyCountryId, DeployType.BUILD));
                        break;
                        
                    case 2: // Build Navy
                        var buildableSea = CountryState.BuildableSea(Faction);
                        int navyCountryId = await new SelectCountryHandler(buildableSea.ToCountryIds()).Handle();
                        result = BuildChangeEvent(new DeployUnitChangeEvent(Faction, navyCountryId, DeployType.BUILD));
                        break;
                        
                    case 3: // Land Battle
                        List<int> armyUnits = UnitState.AttackableArmyIds(Faction);
                        List<int> landCountries = CountryState.AttackableLandIds(Faction);
                        BattleTarget landTarget = await new SelectBattleTargetHandler(landCountries, armyUnits).Handle();
                        result = BuildChangeEvent(landTarget.ToAttackChangeEvent(Faction));
                        break;
                        
                    case 4: // Sea Battle
                        List<int> navyUnits = UnitState.AttackableNavyIds(Faction);
                        List<int> seaCountries = CountryState.AttackableSeaIds(Faction);
                        BattleTarget seaTarget = await new SelectBattleTargetHandler(seaCountries, navyUnits).Handle();
                        result = BuildChangeEvent(seaTarget.ToAttackChangeEvent(Faction));
                        break;
                }
                
                if (result != null)
                    result.IsTrigger = true;
                
                return result;
            })
            .WithGuidance("Use this card to: build an army, build a navy, land battle, or sea battle")
        }; 
    }
}