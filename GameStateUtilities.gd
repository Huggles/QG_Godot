class_name GameStateUtilities

static var game_state:GameState:
	get: return GameManager.game_state

static func active_units_for_faction(_faction:Enum.Faction) -> Array[int]:	
	return game_state.faction_state_for_enum(_faction).active_unit_ids

static func supplied_units_for_faction(_faction:Enum.Faction) -> Array[int]:	
	return game_state.faction_state_for_enum(_faction).supplied_unit_ids

static func get_supply_country_ids(_faction:Enum.Faction) -> Array[int]:
	var response:Array[int] = []
	for country_state in game_state.country_states:
		if country_state.is_supply && country_state.occupying_factions.has(_faction):
			response.push_front(country_state.id)	
	return response

static func active_unit_ids(_faction:Enum.Faction) -> Array[int]:
	var response:Array[int]
	for _unit_state in GameManager.game_state.unit_states:
		if _unit_state.faction_enum == _faction && _unit_state.country_id >= 0: 
			response.push_back(_unit_state.id)
	return response

static func occupied_country_ids(_faction:Enum.Faction) -> Array[int]:
	var response:Array[int]
	for _unit_id in active_unit_ids(_faction):			
		response.push_back(UnitState.for_id(_unit_id).country_id)							
	return response		

static func supplied_unit_ids(_faction:Enum.Faction) -> Array[int]:
	var response:Array[int]
	for _unit_id in active_unit_ids(_faction):		
		var _unit_state:UnitState = UnitState.for_id(_unit_id)
		if _unit_state.in_supply:
			response.push_back(_unit_id)							
	return response		
		
static func unsupplied_unit_ids(_faction:Enum.Faction) -> Array[int]:
	var response:Array[int]
	for _unit_id in active_unit_ids(_faction):		
		var _unit_state:UnitState = UnitState.for_id(_unit_id)
		if !_unit_state.in_supply:
			response.push_back(_unit_id)							
	return response

static func straight_state_for_neighbors(_country_id_1:int, _country_id_2:int) -> StraightState:	
	var _controlling_countries = game_state.straight_states.filter(
		func(_straight_state:StraightState):
			var has_1:bool = _straight_state.controlled_country_id_1 == _country_id_1 && _straight_state.controlled_country_id_2 == _country_id_2
			var has_2:bool = _straight_state.controlled_country_id_1 == _country_id_2 && _straight_state.controlled_country_id_2 == _country_id_1
			return has_1 || has_2
			)
	return _controlling_countries[0] if _controlling_countries.size() > 0 else null
	
static func request_response_card_activation(_game_change_event:GameChangeEvent, _callback:Callable):
	for _faction in Enum.Faction.keys():
		request_response_card_activation_for_faction(_game_change_event, _faction, _callback)
		pass
	
static func request_response_card_activation_for_faction(_game_change_event:GameChangeEvent, _faction:Enum.Faction, _callback:Callable):
	if DeckState.for_faction(_faction).response_card_ids.size() > 0:
		pass
	pass
	
static func request_status_card_activation(_game_change_event:GameChangeEvent, _callback:Callable):
	for _faction in Enum.Faction.keys():
		request_response_card_activation_for_faction(_game_change_event, _faction, _callback)
		pass
	
static func request_status_card_activation_for_faction(_game_change_event:GameChangeEvent, _faction:Enum.Faction, _callback:Callable):
	var _activatable_status_cards:Array[int] = []
	if DeckState.for_faction(_faction).status_card_ids.size() > 0:
		_activatable_status_cards = DeckState.for_faction(_faction).activatable_status_card_ids(_game_change_event)			
	else:
		_callback.call()
	
	
	
