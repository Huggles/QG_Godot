class_name BattleUnitChangeEvent extends RemoveUnitChangeEvent

func _init(_triggering_faction:Enum.Faction, _unit_id:int) -> void:	
	super(_triggering_faction, _unit_id, Enum.UnitRemovalReason.BATTLE)

func display_text() -> String:	
	return str(Enum.Faction.keys()[triggering_faction], " battled ", UnitState.for_id(unit_id).faction, " in ", CountryState.for_id(country_id).clabel)	 
