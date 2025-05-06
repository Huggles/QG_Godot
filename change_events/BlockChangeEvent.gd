class_name BlockChangeEvent extends GameChangeEvent

var triggering_change_event:GameChangeEvent

func _init(_triggering_faction:Enum.Faction, _triggering_change_event:GameChangeEvent) -> void:	
	super(_triggering_faction)	
	self.triggering_change_event = _triggering_change_event	
	
func _apply_change_event():		
	triggering_change_event.blocked = true
	