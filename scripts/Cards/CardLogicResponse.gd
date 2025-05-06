class_name CardLogicResponse extends CardLogicBase  

func _init(_card_state:CardState)->void:	
	super(_card_state)
	
func _can_activate_action(_game_change_event:GameChangeEvent)->bool:	
	DebugUtilities.print_peer_err(str("can_activate_action_for_game_change_event not implemented for ", self.card_data.name)) 
	return false

func _can_play_card(_part:int) -> bool:
	return true

func _play_card(_part:int)->void:	 
	DeckState.for_faction(faction).response_card_ids.push_back(card_state.id)

func _activate_action(_game_change_event:GameChangeEvent, _part:int):
	DebugUtilities.print_peer_err(str("active_card not implemented for ", self.card_data.name))
	pass
	

	
