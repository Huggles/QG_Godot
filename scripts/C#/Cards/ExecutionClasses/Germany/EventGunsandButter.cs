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
                // Build list of available action card IDs
                List<int> availableCardIds = new List<int>();
                
                // Get faction-specific action cards
                int buildArmyId = GameSession.Instance.GameState.CardStates.First(c => c.CardData.UniqueName == "BuildArmy" && c.Faction == Faction).Id;
                int buildNavyId = GameSession.Instance.GameState.CardStates.First(c => c.CardData.UniqueName == "BuildNavy" && c.Faction == Faction).Id;
                int landBattleId = GameSession.Instance.GameState.CardStates.First(c => c.CardData.UniqueName == "LandBattle" && c.Faction == Faction).Id;
                int seaBattleId = GameSession.Instance.GameState.CardStates.First(c => c.CardData.UniqueName == "SeaBattle" && c.Faction == Faction).Id;
                
                if (CountryState.BuildableLand(Faction).Any())
                    availableCardIds.Add(buildArmyId);
                
                if (CountryState.BuildableSea(Faction).Any())
                    availableCardIds.Add(buildNavyId);
                
                if (UnitState.AttackableArmies(Faction).Any() || CountryState.AttackableLand(Faction).Any())
                    availableCardIds.Add(landBattleId);
                
                if (UnitState.AttackableNavies(Faction).Any() || CountryState.AttackableSea(Faction).Any())
                    availableCardIds.Add(seaBattleId);
                
                // Show modal with card visuals
                List<PresentationItem> actionOptions = PresentationItemCard.FromCardIds(availableCardIds, true);
                Variant[] response = await PresentationModal.Instance.ShowModal(actionOptions, "Choose an action");
                int selectedCardId = response[0].As<int>();
                await PresentationModal.Instance.HideModal();
                
                // Execute the chosen action
                ChangeEvent result = null;
                if (selectedCardId == buildArmyId)
                {
                    var buildableLand = CountryState.BuildableLand(Faction);
                    int armyCountryId = await new SelectCountryHandler(buildableLand.ToCountryIds()).Handle();
                    result = BuildChangeEvent(new DeployUnitChangeEvent(Faction, armyCountryId, DeployType.BUILD));
                }
                else if (selectedCardId == buildNavyId)
                {
                    var buildableSea = CountryState.BuildableSea(Faction);
                    int navyCountryId = await new SelectCountryHandler(buildableSea.ToCountryIds()).Handle();
                    result = BuildChangeEvent(new DeployUnitChangeEvent(Faction, navyCountryId, DeployType.BUILD));
                }
                else if (selectedCardId == landBattleId)
                {
                    List<int> armyUnits = UnitState.AttackableArmyIds(Faction);
                    List<int> landCountries = CountryState.AttackableLandIds(Faction);
                    BattleTarget landTarget = await new SelectBattleTargetHandler(landCountries, armyUnits).Handle();
                    result = BuildChangeEvent(landTarget.ToAttackChangeEvent(Faction));
                }
                else if (selectedCardId == seaBattleId)
                {
                    List<int> navyUnits = UnitState.AttackableNavyIds(Faction);
                    List<int> seaCountries = CountryState.AttackableSeaIds(Faction);
                    BattleTarget seaTarget = await new SelectBattleTargetHandler(seaCountries, navyUnits).Handle();
                    result = BuildChangeEvent(seaTarget.ToAttackChangeEvent(Faction));
                }
                
                if (result != null)
                    result.IsTrigger = true;
                
                return result;
            })
            .WithGuidance("Use this card to: build an army, build a navy, land battle, or sea battle")
        }; 
    }
}