class_name EliminateUnitChangeEvent extends RemoveUnitChangeEvent

func _init(_triggering_faction:Enum.Faction, _unit_id:int) -> void:	
	super(_triggering_faction, _unit_id, Enum.UnitRemovalReason.ELIMINATE)
