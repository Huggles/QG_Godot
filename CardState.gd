extends StateObject
class_name CardState

var id:int
var card_data:CardData

var card_execution_class:CardLogicBase:
	get: 
		if card_execution_class == null:
			card_execution_class = card_data.get_card_logic_class()
		return card_execution_class

func _init(_card_data:CardData):
	card_data = _card_data

func can_play_card() -> bool:
	return card_execution_class.can_execute_card()

func execute_card()->void:
	DebugUtilities.print_peer("execute_card")
	card_execution_class.execute_card()
	pass
	
	

	
static func for_id(_card_id:int) -> CardState:	
	return GameManager.game_state.card_states_by_id.get(_card_id)
	
static func for_ids(_card_ids:Array[int]) -> Array[CardState]:
	var response:Array[CardState] = []
	for _card_id in _card_ids:
		response.push_back(for_id(_card_id))	
	return response
