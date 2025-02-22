extends DataObject
class_name GameState

var game_mode:GameMode
var faction_states:Dictionary = {
	Enum.Faction.GERMANY 		: 				FactionState.new(Enum.Faction.GERMANY),
	Enum.Faction.UNITED_KINGDOM 	: 				FactionState.new(Enum.Faction.UNITED_KINGDOM),
	Enum.Faction.JAPAN 			:				FactionState.new(Enum.Faction.JAPAN),
	Enum.Faction.SOVIET 			: 				FactionState.new(Enum.Faction.SOVIET),
	Enum.Faction.ITALY 			: 				FactionState.new(Enum.Faction.ITALY),
	Enum.Faction.UNITED_STATES 	: 				FactionState.new(Enum.Faction.UNITED_STATES),	
}

signal COUNTRY_CLICKED

#List of country states, containing live data about the countries in this game. 
var country_states:Array[CountryState]
var country_state_by_id:Dictionary:
	get: 
		if country_state_by_id || country_state_by_id.size() == 0:
			for country_state in country_states:	
				country_state_by_id[country_state.id] = country_state
		return country_state_by_id
		
var country_state_by_name:Dictionary:
	get: 
		if country_state_by_name || country_state_by_name.size() == 0:
			for country_state in country_states:	
				country_state_by_name[country_state.name] = country_state
		return country_state_by_name
		
		
var unit_states:Array[UnitState]
var unit_states_by_id:Dictionary:
	get: 
		if unit_states_by_id || unit_states_by_id.size() == 0:
			for unit_state in unit_states:	
				unit_states_by_id[unit_state.id] = unit_state
		return unit_states_by_id

func _init() -> void:		
	return

func deploy_unit_to_country(_country_id:int, _faction:Enum.Faction, _unit_type:Enum.UnitType) -> void:	
	if(_country_id == null || _faction == null || _unit_type == null):		
		return
	var unit_id:int = UnitPool.get_available_unit_for_faction(_faction)	
	var unit_state:UnitState = GameManager.game_state.unit_states_by_id[unit_id]		
	var countryState:CountryState = country_state_by_id[_country_id];
	if countryState.is_country_full == false && countryState.can_build(_faction):	
		unit_state.BEFORE_UNIT_DEPLOYED_TO_COUNTRY.emit();
		countryState.units[unit_state.faction_enum] = unit_state.id
		unit_state.country_id = _country_id
		unit_state.AFTER_UNIT_DEPLOYED_TO_COUNTRY.emit();
	return	
		
func attack_unit(_unit_id:int) -> void:	
	if(_unit_id == null):		
		return		
	var unit_state:UnitState = unit_states_by_id[_unit_id];	
	unit_state.BEFORE_UNIT_REMOVED_FROM_COUNTRY.emit();
	unit_state.country_state.units.erase(unit_state.faction_enum)
	unit_state.country_id = -1;
	unit_state.AFTER_UNIT_REMOVED_FROM_COUNTRY.emit();
	return	
	
func eliminate_unit(unit_id:String) -> void:	
	return
	
func score_victory_points(_faction:Enum.Faction, _victory_points:int) -> void:	
	var fs:FactionState = self.faction_states[_faction]
	fs.score += _victory_points
	return

func hand_discard(_card_ids:Array[String]) -> void:	
	return

func force_discard(_card_id:String) -> void:	
	return
		
func force_draw_deck_discard(_faction:String, _number_of_cards:int) -> void:	
	return

func play_card(_card_id:String) -> void:	
	return
	
func activate_status_card(_card_id:String) -> void:	
	return

func activate_response_card(_card_id:String) -> void:	
	return
	
func request_country_selection(_faction:Enum.Faction, callback:Callable):
	var country_state:CountryState = Globals.countries_by_name["US_EAST"];
	
	
func _set_countries_selectable(country_states:Array[CountryState], _faction:Enum.Faction, callback:Callable):
	for country_state:CountryState in country_states:
		GameManager.my_camera.enable_ray_trace_casting()
		country_state.set_clickable(
			func(country_state:CountryState): 
				print("Clicked: "+ country_state.clabel)
		
	)
	
	
