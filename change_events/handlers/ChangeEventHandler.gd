class_name ChangeEventHandler extends IGameEventHandler

var change_event:GameChangeEvent
var is_trigger:bool

func _init(_change_event:GameChangeEvent, _is_trigger:bool = false) -> void:
	self.change_event = _change_event
	self.is_trigger = _is_trigger

static var _change_event_counter:int = 0
static func execute_change_event(_change_event:GameChangeEvent, _is_trigger:bool = true) -> ChangeEventHandler:	
	print(str("ChangeEventHandler ", _change_event.trace_text()))
	var change_event_handler:ChangeEventHandler = ChangeEventHandler.new(_change_event, _is_trigger)	
	_change_event.id = _change_event_counter;
	_change_event_counter += 1;
	_change_event.handler = change_event_handler
	_change_event.is_trigger = _is_trigger
	GameManager.game_state.game_change_events.push_back(_change_event)
	change_event_handler._execute_change_event()
	return change_event_handler

func _execute_change_event():
	EventBusLocal.game_change_event_before.emit(self.change_event.id)	
	change_event.apply_change()
	await GameManager.create_timer(200)
	EventBusLocal.game_change_event_after.emit(self.change_event.id)



