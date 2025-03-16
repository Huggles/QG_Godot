class_name InputHandlerDebug extends IInputHandler

const HAND_CARD_KEYS = [KEY_0,KEY_1,KEY_2,KEY_3,KEY_4,KEY_5,KEY_6]

func _init() -> void:
	pass

func on_key_clicked(_key_event:InputEventKey):
	if _key_event.keycode == KEY_1:
		GameManager.game_flow.progress_game()
		
	if _key_event.keycode == KEY_2:
		var _faction_state:FactionState = GameManager.game_state.faction_states[GameManager.game_flow.current_faction]
		_faction_state.deck[0].play_card()		
		
	if _key_event.keycode == KEY_3:
		var _faction_state:FactionState = GameManager.game_state.faction_states[GameManager.game_flow.current_faction]
		_faction_state.deck[1].play_card()
			
	if _key_event.keycode == KEY_4:
		EventBusLocal.recalculate_supply.emit()
	
