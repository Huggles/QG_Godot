class_name InputHandlerPlayCard extends IInputHandler

const HAND_CARD_KEYS = [KEY_0,KEY_1,KEY_2,KEY_3,KEY_4,KEY_5,KEY_6]

func _init() -> void:	

	var _current_faction:Enum.Faction = GameManager.game_flow.current_faction
	var _faction_color_string:String = GameManager.game_state.faction_state_for_enum(_current_faction).faction_data.color_string

	var _text_lines:Array[String] = []
	_text_lines.push_back("[color=%s][b]%s: Select a card to play:[/b][/color]" % [_faction_color_string, Enum.Faction.keys()[_current_faction]])



	var index = 0
	for _card_state:CardState in DeckState.for_faction(GameManager.game_flow.current_faction).hand_card_states:
		if _card_state.can_play_card():
			_text_lines.push_back(str(index, " - ", _card_state.card_data.name))
		else:
			_text_lines.push_back(str("[color=red]",index, " - ", _card_state.card_data.name, "[/color]"))
		index += 1
		pass
	InputMessageLabel.show_text("\n".join(_text_lines))

func on_key_clicked(_key_event:InputEventKey):
	for key in HAND_CARD_KEYS:		
		if _key_event.keycode == key:
			var index = HAND_CARD_KEYS.find(key)			
			if GameManager.game_flow.current_faction_deck_state.hand_card_states[index].can_play_card():
				GameManager.my_input_manager.set_no_input_active()
				InputMessageLabel.hide_node()
				GameManager.game_flow.current_faction_deck_state.play_card_at_hand_index(index, func():)
			return
	
