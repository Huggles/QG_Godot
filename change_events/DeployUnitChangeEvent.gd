class_name DeployUnitChangeEvent extends GameChangeEvent 

var unit_id:int
var country_id:int
var deployment_type:Enum.DeployType
var unit_type:Enum.UnitType:
	get: 
		var country:CountryState = CountryState.for_id(country_id)
		return Enum.UnitType.ARMY if country.type == Enum.CountryType.LAND else Enum.UnitType.NAVY

func _init(_triggering_faction:Enum.Faction, _country_id:int, _deployment_type:Enum.DeployType) -> void:	
	super(_triggering_faction)	
	self.country_id 			= _country_id
	self.deployment_type 	= _deployment_type	
	
func _apply_change_event():	
	print("_apply_change_event")
	GameManager.game_state.deploy_unit_to_country(country_id, triggering_faction, unit_type, deployment_type)
	

	
func trace_text() -> String:
	return str(script_name,"-", Enum.Faction.keys()[triggering_faction], "-",CountryState.for_id(country_id).clabel, "-", Enum.UnitType.keys()[unit_type])


func display_text() -> String:				
	var _unit_state
	return str("Deployed ", Enum.Faction.keys()[triggering_faction]," ", Enum.UnitType.keys()[unit_type], " to country: ", CountryState.for_id(country_id).clabel)

	
	
