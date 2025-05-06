class_name CardState extends StateObject

var id:int
var card_data:CardData
var faction:Enum.Faction
var card_name:String:
	get: return card_data.clabel if card_data != null else ""

var triggered_by_change_event:GameChangeEvent

var card_execution_class:CardLogicBase:
	get: 
		if card_execution_class == null:
			card_execution_class = get_card_logic_class()
		return card_execution_class

func _init(_card_data:CardData):
	card_data = _card_data

func can_play_card() -> bool:
	if card_execution_class != null: 
		return card_execution_class.can_play_card()
	return false

func play_card()->void:
	DebugUtilities.print_peer("play_card")
	card_execution_class.play_card()
	pass

func get_card_logic_class()->CardLogicBase:	
	if DataUtilities.class_map.has(card_data.execution_class):
		var class_path = DataUtilities.class_map.get(card_data.execution_class).path
		var instance:CardLogicBase = load(class_path).new(self)		
		return instance
	else:		
		DebugUtilities.print_peer_err(str("Could not find card logic class for: ", card_data.clabel))
		return null
	
static func for_id(_card_id:int) -> CardState:	
	return GameManager.game_state.card_states_by_id.get(_card_id)
	
static func for_ids(_card_ids:Array[int]) -> Array[CardState]:
	var response:Array[CardState] = []
	for _card_id in _card_ids:
		response.push_back(for_id(_card_id))	
	return response

static func for_name(_card_name:String) -> CardState:
	return GameManager.game_state.card_states_by_name.get(_card_name)
