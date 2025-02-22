extends Object
class_name UnitPool

static var unit_counter = -1;

static func get_available_unit_for_faction(_faction:Enum.Faction) -> int:
	var unit_states:Array[UnitState] = []
	unit_states.assign(Globals.unit_states_by_faction_enum[_faction])
	for unit_state in unit_states:
		if(unit_state.is_deployed_to_country == false):
			return unit_state.id		
			
	printerr("COULDNT FIND UNIT IN UNIT POOL")
	return -1

static func get_unique_unit_id() -> int:
	unit_counter += 1
	return unit_counter
