extends Object
class_name UnitPool

static var unit_counter = -1;

static func get_available_unit_for_faction(_faction:Enum.Faction, _unit_type:Enum.UnitType) -> int:	
	var _unit_states:Array[UnitState] = UnitState.for_ids(GameStateUtilities.game_state.faction_state_for_enum(_faction).all_units)
	for _unit_state in _unit_states:
		if(_unit_state.is_deployed_to_country == false && _unit_state.type == _unit_type):
			return _unit_state.id		
			
	printerr("COULDNT FIND UNIT IN UNIT POOL")
	return -1

static func get_unique_unit_id() -> int:
	unit_counter += 1
	return unit_counter


static func faction_has_available_army(_faction:Enum.Faction):	
	return faction_has_available_units(_faction, Enum.UnitType.ARMY)
	
static func faction_has_available_navy(_faction:Enum.Faction):	
	return faction_has_available_units(_faction, Enum.UnitType.NAVY)

static func faction_has_available_units(_faction:Enum.Faction, _unit_type:Enum.UnitType):	
	print("faction_has_available_units")
	var _unit_states:Array[UnitState] = UnitState.for_ids(GameStateUtilities.game_state.faction_state_for_enum(_faction).all_units)	
	var _available_unit_states:Array[UnitState] = _unit_states.filter(
		func(_unit_state:UnitState): 
			return _unit_state.type == _unit_type && _unit_state.is_deployed_to_country == false
			)	
	return _available_unit_states.size() > 0
