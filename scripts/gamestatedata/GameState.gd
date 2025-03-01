extends DataObject
class_name GameState

var game_mode:GameMode
var faction_states:Dictionary
var game_change_events:Array[GameChangeEvent]

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

var card_states:Array[CardState]
var card_states_by_id:Dictionary:
	get: 
		if card_states_by_id || card_states_by_id.size() == 0:
			for card_state in card_states:	
				card_states_by_id[card_state.id] = card_state
		return card_states_by_id

func _init() -> void:		
	return
	
func faction_state_for_enum(_faction:Enum.Faction) -> FactionState:
	return faction_states[_faction]

func deploy_unit_to_country(_country_id:int, _faction:Enum.Faction, _unit_type:Enum.UnitType) -> void:	
	if(_country_id == null || _faction == null || _unit_type == null):		
		return
	var _unit_id:int = UnitPool.get_available_unit_for_faction(_faction,_unit_type)	
	var _unit_state:UnitState = UnitState.for_id(_unit_id)
	var countryState:CountryState = country_state_by_id[_country_id];
	if countryState.is_country_full == false && countryState.can_build(_faction):	
		_unit_state.BEFORE_UNIT_DEPLOYED_TO_COUNTRY.emit();
		countryState.units[_unit_state.faction_enum] = _unit_state.id
		_unit_state.country_id = _country_id
		_unit_state.AFTER_UNIT_DEPLOYED_TO_COUNTRY.emit();
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
	
func eliminate_unit(_unit_id:String) -> void:	
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
	
func buildable_countries_for_faction(_faction:Enum.Faction) -> Array[int]:
	var response:Array[int]	= []
	var supplied_unit_ids = GameStateUtilities.supplied_units_for_faction(_faction)	
	if supplied_unit_ids.size() > 0:
		for _country_state in CountryState.for_unit_ids(supplied_unit_ids):
			for _neighbor_country_state in _country_state.neighbor_country_states:
				if _neighbor_country_state.can_build(_faction):
					response.push_back(_neighbor_country_state.id)
	return response
	
func request_single_country_selection(_countries:Array[CountryState], _faction:Enum.Faction, _callback:Callable):	
	if _countries.size() == 0: 
		DebugUtilities.print_peer_err("No countries provided to select")
		_callback.call(-1)
		
	GameManager.game_state._set_countries_selectable(
		_countries,
		_faction, 
		func(_selected_country:CountryState):
			print(_selected_country.clabel)
			_set_all_countries_unselectable(_faction)	
			_callback.call(_selected_country.id)				
			)
	
func _set_countries_selectable(_country_states:Array[CountryState], _faction:Enum.Faction, _callback:Callable):
	for _country_state:CountryState in _country_states:
		GameManager.my_input_manager.enable_ray_trace_casting()
		_country_state.set_clickable(_callback)

func _set_all_countries_unselectable(_faction:Enum.Faction):
		for _country_state:CountryState in GameManager.game_state.country_states:
			GameManager.my_input_manager.disable_ray_trace_casting()
			_country_state.set_unclickable()

func _set_units_selectable(_unit_states:Array[UnitState], _faction:Enum.Faction, _callback:Callable):
	for _unit_state:UnitState in _unit_states:
		GameManager.my_input_manager.enable_ray_trace_casting()
		_unit_state.set_clickable(
			func(_clicked_unit_state:UnitState): 
				_clicked_unit_state.debug()
		
	)
	
	
