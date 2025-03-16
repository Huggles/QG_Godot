class_name GameChangeEvent extends Object

var script_name:String:
	get: return self.get_script().get_global_name()
var id:int = -1
var handler:ChangeEventHandler

var callback:Callable
var triggering_faction:Enum.Faction
var suppress_game_progress = false
var can_trigger_response:bool = false
var can_trigger_status:bool = false
var source_card_id:int = -1;
var has_source_card:int: 
	get: return source_card_id > -1
var source_card_state:CardState:
	get: return CardState.for_id(source_card_id)

func _init(_triggering_faction:Enum.Faction) -> void:
	self.triggering_faction = _triggering_faction
	pass
	
#Should only be called by ChangeEventHandler
func apply_change(_callback:Callable):	
	self.callback = _callback
	_apply_change_event()	
	EventBusLocal.game_change_event_occurred.emit(self)		
	if can_trigger_status:
		pass
	_callback.call(self)
	
func _apply_change_event():
	pass
	
func trace_text() -> String:
	return script_name	 

func display_text() -> String:	
	return script_name	
