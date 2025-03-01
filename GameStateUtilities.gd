class_name GameStateUtilities

static var game_state:GameState:
	get: return GameManager.game_state

static func active_units_for_faction(_faction:Enum.Faction) -> Array[int]:	
	return game_state.faction_state_for_enum(_faction).active_unit_ids

static func supplied_units_for_faction(_faction:Enum.Faction) -> Array[int]:	
	return game_state.faction_state_for_enum(_faction).supplied_unit_ids
	
static func recalculate_supply():
	for _faction in Enum.Faction.values():		
		recalculate_supply_for_faction(_faction)

static func recalculate_supply_for_faction(_faction:Enum.Faction):
	var _faction_state:FactionState = game_state.faction_states[_faction]
	var _path_finding_service:PathFindingService = PathFindingService.new(IPathFindingNode.new(), _faction)	
	var _active_units_for_faction = UnitState.for_ids(_faction_state.active_unit_ids)
	var _active_army_units_for_faction = _active_units_for_faction.filter(func(_us:UnitState): return _us.type == Enum.UnitType.ARMY )
	var _active_navy_units_for_faction = _active_units_for_faction.filter(func(_us:UnitState): return _us.type == Enum.UnitType.NAVY )
	for _unit_state:UnitState in _active_army_units_for_faction:
		var _unit_supplied:bool = _recalculate_supply_for_unit_id(_path_finding_service, _unit_state.id)						
		if _unit_supplied: 
			_unit_state.set_in_supply()
			continue			
		else:
			_unit_state.set_out_of_supply()
	
	for _unit_state:UnitState in _active_navy_units_for_faction:
		var _unit_supplied:bool = _recalculate_supply_for_unit_id(_path_finding_service, _unit_state.id)						
		if _unit_supplied: 
			_unit_state.set_in_supply()
			continue			
		else:
			_unit_state.set_out_of_supply()

static func _recalculate_supply_for_unit_id(_path_finding_service:PathFindingService, _unit_id:int):
	var _unit_state:UnitState = UnitState.for_id(_unit_id)
	for _supply_country_id in get_supply_country_ids(_unit_state.faction_enum):
		DebugUtilities.print_peer(str("Finding path from: ", CountryState.for_id(_unit_state.country_id).clabel," to supply country: ", CountryState.for_id(_supply_country_id).clabel))
		var has_path:bool = _path_finding_service.calculate_path(_unit_state.country_id,_supply_country_id)				
		if has_path: return true
	DebugUtilities.print_peer(str("Unit is now out of supply in ", _unit_state.country_state.clabel ," (",_unit_state.faction, ")"))
	return false		
	
static func register_game_change_event(game_change_event:GameChangeEvent) -> void:
	game_state.game_change_events.push_back(game_change_event)

static func get_supply_country_ids(_faction:Enum.Faction) -> Array[int]:
	var response:Array[int] = []
	for country_state in game_state.country_states:
		if country_state.is_supply && country_state.occupying_factions.has(_faction):
			response.push_front(country_state.id)	
	return response

func active_unit_ids(_faction:Enum.Faction) -> Array[int]:
	var response:Array[int]
	for _unit_state in GameManager.game_state.unit_states:
		if _unit_state.faction_enum == _faction && _unit_state.country_id >= 0: 
			response.push_back(_unit_state.id)
	return response

func occupied_country_ids(_faction:Enum.Faction) -> Array[int]:
	var response:Array[int]
	for _unit_id in active_unit_ids(_faction):			
		response.push_back(UnitState.for_id(_unit_id).country_id)							
	return response		

func supplied_unit_ids(_faction:Enum.Faction) -> Array[int]:
	var response:Array[int]
	for _unit_id in active_unit_ids(_faction):		
		var _unit_state:UnitState = UnitState.for_id(_unit_id)
		if _unit_state.in_supply:
			response.push_back(_unit_id)							
	return response		
		
func unsupplied_unit_ids(_faction:Enum.Faction) -> Array[int]:
	var response:Array[int]
	for _unit_id in active_unit_ids(_faction):		
		var _unit_state:UnitState = UnitState.for_id(_unit_id)
		if !_unit_state.in_supply:
			response.push_back(_unit_id)							
	return response
