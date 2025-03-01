extends GameChangeEvent
class_name DeployUnitChangeEvent

var unit_id:int
var country_id:int
var deployment_type:Enum.DeployType
var unit_type:Enum.UnitType:
	get: 
		var country:CountryState = CountryState.for_id(country_id)
		return Enum.UnitType.ARMY if country.type == Enum.CountryType.LAND else Enum.UnitType.NAVY

func _init(_triggering_faction:Enum.Faction, _country_id:int, _deployment_type:Enum.DeployType) -> void:
	self.event_type 			= "DEPLOY_UNIT"
	self.triggering_faction  = _triggering_faction	
	self.country_id 			= _country_id
	self.deployment_type 	= _deployment_type
	
func _apply_change_event():	
	GameManager.game_state.deploy_unit_to_country(country_id, triggering_faction, unit_type)
	GameStateUtilities.recalculate_supply()	


	
	
