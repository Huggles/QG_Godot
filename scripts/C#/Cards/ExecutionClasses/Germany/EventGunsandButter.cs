using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class EventGunsAndButter : EventCardLogic
{
    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async() => {
                // Build list of available action card IDs
                List<int> availableCardIds = new List<int>();
                
                // Get faction-specific action cards
                int buildArmyId = GameSession.Current.GameState.CardStates.First(c => c.CardData.UniqueName == "BuildArmy" && c.Faction == Faction).Id;
                int buildNavyId = GameSession.Current.GameState.CardStates.First(c => c.CardData.UniqueName == "BuildNavy" && c.Faction == Faction).Id;
                int landBattleId = GameSession.Current.GameState.CardStates.First(c => c.CardData.UniqueName == "LandBattle" && c.Faction == Faction).Id;
                int seaBattleId = GameSession.Current.GameState.CardStates.First(c => c.CardData.UniqueName == "SeaBattle" && c.Faction == Faction).Id;
                
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
                Variant[] response = await PresentationModal.Current.ShowModal(actionOptions, "Choose an action");
                int selectedCardId = response[0].As<int>();
                await PresentationModal.Current.HideModal();
                
                // Execute the chosen action
                ChangeEvent result = null;
                if (selectedCardId == buildArmyId)
                {
                    var buildableLand = CountryState.BuildableLand(Faction);
                    int armyCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, buildableLand.ToCountryIds()).BroadCast()).ResponseCountryIds[0];
                    result = BuildChangeEvent(new DeployUnitChangeEvent(Faction, armyCountryId, DeployType.BUILD));
                }
                else if (selectedCardId == buildNavyId)
                {
                    var buildableSea = CountryState.BuildableSea(Faction);
                    int navyCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, buildableSea.ToCountryIds()).BroadCast()).ResponseCountryIds[0];
                    result = BuildChangeEvent(new DeployUnitChangeEvent(Faction, navyCountryId, DeployType.BUILD));
                }
                else if (selectedCardId == landBattleId)
                {
                    List<int> armyUnits = UnitState.AttackableArmyIds(Faction);
                    List<int> landCountries = CountryState.AttackableLandIds(Faction);
                    var landResp = await new InputRequest.SelectBattleTargetRequestHandler(Faction, landCountries, armyUnits).BroadCast();
                    BattleTarget landTarget = landResp.ResponseCountryIds.Count > 0
                        ? new BattleTarget(landResp.ResponseCountryIds[0], TargetType.COUNTRY)
                        : new BattleTarget(landResp.ResponseUnitIds[0], TargetType.UNIT);
                    result = BuildChangeEvent(landTarget.ToAttackChangeEvent(Faction));
                }
                else if (selectedCardId == seaBattleId)
                {
                    List<int> navyUnits = UnitState.AttackableNavyIds(Faction);
                    List<int> seaCountries = CountryState.AttackableSeaIds(Faction);
                    var seaResp = await new InputRequest.SelectBattleTargetRequestHandler(Faction, seaCountries, navyUnits).BroadCast();
                    BattleTarget seaTarget = seaResp.ResponseCountryIds.Count > 0
                        ? new BattleTarget(seaResp.ResponseCountryIds[0], TargetType.COUNTRY)
                        : new BattleTarget(seaResp.ResponseUnitIds[0], TargetType.UNIT);
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