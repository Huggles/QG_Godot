class_name CardLogicStatus extends CardLogicBase

func _init(_card_state:CardState)->void:	
	super(_card_state)

func can_play_card() -> bool:
	return true

func _play_card()->void:	 
	DeckState.for_faction(faction).status_card_ids.push_back(card_state.id)



	
func _can_activate_card(_game_change_event:GameChangeEvent)->bool:	
	DebugUtilities.print_peer_err(str("can_activate_card_for_game_change_event not implemented for ", self.card_data.name))
	return false

func _activate_card(_game_change_event:GameChangeEvent):
	DebugUtilities.print_peer_err(str("active_card not implemented for ", self.card_data.name))
	pass
	

	
