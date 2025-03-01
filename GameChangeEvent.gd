extends Object
class_name GameChangeEvent

var event_type:String
var triggering_faction:Enum.Faction

var can_trigger_response:bool = false
var can_trigger_status:bool = false

func _init(_event_type:String):
	self.event_type = _event_type
	
func apply_change():	
	GameStateUtilities.register_game_change_event(self)
	if GameManager.game_flow != null:
		GameManager.game_flow.CHANGE_EVENT_ADDED.emit(self)
	_apply_change_event()
	
func _apply_change_event():
	pass
