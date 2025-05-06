class_name InputHandlerActivateBeforeCard extends IInputHandler

var keys  = []
var faction:Enum.Faction

var activation_options:Array[CardActivationOption]
var game_change_event:GameChangeEvent

func _calculate_activation_options():
	activation_options = DeckState.for_faction(self.faction).activatable_cards_for_gce(self.game_change_event, true)

func _init(_faction:Enum.Faction, _game_change_event:GameChangeEvent) -> void:	
	self.faction = _faction
	self.game_change_event = _game_change_event
	_calculate_activation_options()
	
	var _text_lines:Array[String] = []
	_text_lines.push_back("Select a status or response card to play before action:")
	_text_lines.push_back("Q - Do not play a card")
	_text_lines.push_back(str("\n"))

	var _faction_deck_state:DeckState = DeckState.for_faction(_faction)	
	var index = 0
	for _activation_option:CardActivationOption in activation_options:
		var _key:Key = _number_to_key(index)
		keys.push_back(_key)
		var _gce:GameChangeEvent = GameChangeEvent.for_id(_activation_option.change_event_id)
		DebugUtilities.print_peer(_gce.id)
		DebugUtilities.print_peer(_gce.summary_text())
		_text_lines.push_back(str(index, " - ", CardState.for_id(_activation_option.card_id).card_data.clabel,"\n          (", _gce.summary_text(),")"))
		_text_lines.push_back(str("\n"))
		index += 1

	DebugUtilities.print_peer(str("Requesting BEFORE card activation for ", Enum.Faction.keys()[_faction]))
	InputMessageLabel.show_text("\n".join(_text_lines))	
	PlayerActionLabel.show_text("Choose a status or response to activate")
	pass

func on_key_clicked(_key_event:InputEventKey):	
	if _key_event.keycode == KEY_Q:
		GameManager.my_input_manager.set_no_input_active()
		InputMessageLabel.hide_node()
		EventBusLocal.card_selected.emit(NoCardActivatedOption.new("none"))
		return 

	for key in keys:				
		if _key_event.keycode == key:
			var index = keys.find(key)		
			var _activation_option:CardActivationOption = activation_options[index]
			GameManager.my_input_manager.set_no_input_active()		
			InputMessageLabel.hide_node()
			EventBusLocal.card_selected.emit(_activation_option)
			
				
	
