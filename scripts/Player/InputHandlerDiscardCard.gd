class_name InputHandlerDiscardCard extends IInputHandler

const HAND_CARD_KEYS = [KEY_0,KEY_1,KEY_2,KEY_3,KEY_4,KEY_5,KEY_6]

func _init() -> void:
	pass

func on_key_clicked(_key_event:InputEventKey):
	if _key_event.keycode == KEY_ENTER:
		GameManager.my_input_manager.set_no_input_active()
		GameManager.game_flow.progress_game()		
		return
	for key in HAND_CARD_KEYS:		
		if _key_event.keycode == key:
			var index = HAND_CARD_KEYS.find(key)
			if index < GameManager.game_flow.current_faction_deck_state.hand_card_ids.size(): 
				GameManager.game_flow.current_faction_deck_state.discard_card_at_hand_index(index)
				GameManager.game_flow.current_faction_deck_state.debug_hand()
				return	
	
