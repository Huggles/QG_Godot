class_name ChangeEventHandler extends IGameEventHandler

signal change_event_finished
signal status_card_activation_response


var id:int
var change_event:GameChangeEvent
func _init(_change_event:GameChangeEvent) -> void:
	self.change_event = _change_event

static var _change_event_counter:int = 0
static func execute_change_event(_change_event:GameChangeEvent) -> ChangeEventHandler:	
	print(str("ChangeEventHandler ", _change_event.trace_text()))
	var change_event_handler:ChangeEventHandler = ChangeEventHandler.new(_change_event)	
	_change_event.id = _change_event_counter;
	_change_event_counter += 1;
	_change_event.handler = change_event_handler
	change_event_handler._execute_change_event()
	return change_event_handler

func _execute_change_event():	
	#print(change_event.trace_text())
	change_event.apply_change(_handle_game_change_event)
	pass

func _handle_game_change_event(_change_event:GameChangeEvent):		
	if _change_event.has_source_card:
		await _request_status_card_activation()
	
	if StaticGameData.other_faction_team_for_faction(_change_event.triggering_faction):
		pass	
	
	await GameManager.create_timer(200)
	change_event_finished.emit()

func _request_status_card_activation():	
	for _faction in Enum.Faction.values():
		await _request_status_card_activation_for_faction(_faction)

func _request_status_card_activation_for_faction( _faction:Enum.Faction):
	DebugUtilities.print_peer(str("Requesting status card for: ", change_event.trace_text(), ' to ' + Enum.Faction.keys()[_faction]))	

	var _activatable_status_card_ids:Array[int] = DeckState.for_faction(_faction).activatable_status_card_ids(change_event)
	if _activatable_status_card_ids.size() > 0:		
		GameManager.my_input_manager.set_play_status_card_input_active(change_event, _faction)
		var _selected_status_card_id = await EventBusLocal.status_card_activation_started
		while(_selected_status_card_id>-1):
			var _activated_card_id = await EventBusLocal.status_card_activation_completed	
			if _selected_status_card_id == _activated_card_id || _activated_card_id == -1:
				break;
			
	else:
		DebugUtilities.print_peer(str("Player does not have playable status card"))
	
	await GameManager.create_timer(200)
	return

