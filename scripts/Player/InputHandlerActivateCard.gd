class_name InputHandlerActivateCard extends IInputHandler

var status_card_keys  = []
var game_change_event:GameChangeEvent
var faction:Enum.Faction


func _init(_game_change_event:GameChangeEvent, _faction:Enum.Faction) -> void:	
	self.game_change_event = _game_change_event
	
	var _text_lines:Array[String] = []
	_text_lines.push_back("Select a card to activate:")
	_text_lines.push_back("Q - Do not play a status card")

	var _faction_deck_state:DeckState = DeckState.for_faction(_faction)
	var _activatable_status_cards = _faction_deck_state.activatable_status_card_ids(_game_change_event)
	var index = 0
	for _card_number:int in _activatable_status_cards.size():
		status_card_keys.push_back(_number_to_key(_card_number))
		_text_lines.push_back(str(index, " - ", CardState.for_id(_activatable_status_cards[_card_number]).card_data.name))
		index += 1

	DebugUtilities.print_peer(str("Requesting card activation for ", Enum.Faction.keys()[_faction]))
	DeckState.for_faction(_faction).debug_activatable_status_cards(_game_change_event)
	InputMessageLabel.show_text("\n".join(_text_lines))	
	pass

func on_key_clicked(_key_event:InputEventKey):	
	if _key_event.keycode == KEY_Q:
		GameManager.my_input_manager.set_no_input_active()
		InputMessageLabel.hide_node()
		EventBusLocal.status_card_activation_started.emit(-1)
		return 

	for key in status_card_keys:				
		if OS.get_keycode_string(_key_event.keycode) == str(key):
			var index = status_card_keys.find(key)		
			var _card_id:int = DeckState.for_faction(faction).activatable_status_card_ids(game_change_event)[index]
			if _card_id >= 0:				
				GameManager.my_input_manager.set_no_input_active()		
				InputMessageLabel.hide_node()
				GameManager.game_flow.current_faction_deck_state.activate_status_card(_card_id, game_change_event)
				return 
	
