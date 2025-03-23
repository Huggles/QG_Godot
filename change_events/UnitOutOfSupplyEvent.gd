class_name UnitOutOfSupplyEvent extends RemoveUnitChangeEvent

func _init(_unit_id:int) -> void:		
	super(UnitState.for_id(_unit_id).faction_enum, _unit_id, Enum.UnitRemovalReason.SUPPLY)
	
func summary_text() -> String:
	return str(unit_state.faction," out of supply in ", CountryState.for_id(country_id).clabel)	 

func debug_text() -> String:	
	return str(UnitState.for_id(unit_id).faction, " lost unit in ", CountryState.for_id(country_id).clabel, " due to supply")	
