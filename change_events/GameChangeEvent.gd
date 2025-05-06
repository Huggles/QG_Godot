class_name GameChangeEvent extends Object

var script_name:String:
	get: return self.get_script().get_global_name()
var id:int = -1
var handler:ChangeEventHandler

var triggering_faction:Enum.Faction
var suppress_game_progress = false
var is_trigger:bool = true
var source_card_id:int = -1;
var has_source_card:int: 
	get: return source_card_id > -1
var source_card_state:CardState:
	get: return CardState.for_id(source_card_id)

var blocked:bool

signal finished

func _init(_triggering_faction:Enum.Faction) -> void:
	self.triggering_faction = _triggering_faction
	pass
	
#Should only be called by ChangeEventHandler
func apply_change():	
	_apply_change_event()	
	EventBusLocal.game_change_event_occurred.emit(self)		
	EventBusLocal.recalculate_straights.emit()
	EventBusLocal.recalculate_supply.emit()	
	self.finished.emit()
	
func _apply_change_event():
	pass
	
func trace_text() -> String:
	return script_name	 

func summary_text() -> String:	
	return script_name	

func debug_text() -> String:	
	return script_name	

static func for_id(_change_event_id) -> GameChangeEvent:
	return GameManager.game_state.game_change_events.filter(func(_ce): return _ce.id == _change_event_id)[0]

