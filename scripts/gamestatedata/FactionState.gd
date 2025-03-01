extends StateObject
class_name FactionState

var faction_data:FactionData
var faction:Enum.Faction:
	get: return faction_data.faction_enum
var faction_label:String:
	get: return Enum.Faction.keys()[faction]
var score = 0;
var deck_card_ids:Array[int]
var deck_card_states:Array[CardState]:
	get: 
		var _card_states:Array[CardState] = []
		for _card_id in deck_card_ids:
			_card_states.push_back(GameManager.game_state.card_states_by_id[_card_id])
		return _card_states

var hand_card_ids:Array[int]
var hand_card_states:Array[CardState]:
	get:
		var _card_states:Array[CardState] = []
		for _card_id in hand_card_ids:
			_card_states.push_back(GameManager.game_state.card_states_by_id[_card_id])
		return _card_states
	
var discarded_card_ids:Array[int]
var discarded_card_states:Array[CardState]:
	get: 
		var _card_states:Array[CardState] = []
		for _card_id in discarded_card_ids:
			_card_states.push_back(GameManager.game_state.card_states_by_id[_card_id])
		return _card_states

var all_units:Array[int]:
	get:		
		var response:Array[int]
		for _unit_state in GameManager.game_state.unit_states:
			if _unit_state.faction_enum == faction:
				response.push_back(_unit_state.id)
		return response
		
var active_unit_ids:Array[int]:
	get:
		var response:Array[int]
		for _unit_state in UnitState.for_ids(all_units):
			if _unit_state.country_id >= 0: 
				response.push_back(_unit_state.id)
		return response
		
var occupied_country_ids:Array[int]:
	get:
		var response:Array[int]
		for _unit_id in active_unit_ids:			
			response.push_back(GameManager.game_state.unit_states_by_id[_unit_id].country_id)							
		return response

var supplied_unit_ids:Array[int]:
	get:
		var response:Array[int]
		for _unit_id in active_unit_ids:		
			var _unit_state:UnitState = GameManager.game_state.unit_states_by_id[_unit_id]
			if _unit_state.in_supply:
				response.push_back(_unit_id)							
		return response
		
var unsupplied_unit_ids:Array[int]:
	get:
		var response:Array[int]
		for _unit_id in active_unit_ids:		
			var _unit_state:UnitState = GameManager.game_state.unit_states_by_id[_unit_id]
			if !_unit_state.in_supply:
				response.push_back(_unit_id)							
		return response



func _init(_faction_data:FactionData) -> void:
	self.faction_data = _faction_data;
	self.score = 0	
	return

func _init_deck()->void:
	self.deck = [];
	for _card in self.faction_data.cards:
		pass
	
	pass

func draw_top_card() -> int:	
	if deck_card_ids.size() > 0:
		var _top_card_id = deck_card_ids.pop_front()	
		hand_card_ids.push_back(_top_card_id)
		DebugUtilities.print_peer(str("Drew Card (",faction_label,"): ",CardState.for_id(_top_card_id).card_data.name ) )		
		return _top_card_id
	else:
		DebugUtilities.print_peer_err(str("Deck is empty for: ", faction) )		
		return -1
	
func has_card_for_name(_card_name:String) -> bool:
	return deck_card_states.find(func(_card_state:CardState): return _card_state.card_data.name == _card_name) > -1
	
func draw_card_by_name(_card_name:String) -> int:	
	var _deck_card_state_for_name_index:int = deck_card_states.find(func(_card_state:CardState): return _card_state.card_data.name == _card_name)
	var _card_id = deck_card_ids.pop_at(_deck_card_state_for_name_index)	
	
	DebugUtilities.print_peer_err( str("Drew Card by name (", faction_label, "): ",CardState.for_id(_card_id).card_data.clabel ) )		
	return _card_id
	
func draw_cards(number:int) -> Array[int]:
	var response:Array[int] 
	for i in number:
		var _top_card_id = draw_top_card()
		if _top_card_id > 0:
			response.push_back(_top_card_id)	
	return response

func play_card_at_hand_index(_hand_index, _callback:Callable) -> void:
	if _hand_index >= hand_card_ids.size():
		return
	var _card_id = hand_card_ids[_hand_index]
	play_card(_card_id, _callback)

func play_card(_card_id, _callback:Callable) -> void:
	var _card_state:CardState = CardState.for_id(_card_id)
	if !self.hand_card_ids.has(_card_id):		
		DebugUtilities.print_peer_err(str("Card not in hand: ", _card_state.card_data.clabel ) )		
		return
	if !_card_state.can_play_card():
		DebugUtilities.print_peer_err(str("Card not play card: ", _card_state.card_data.clabel ) )		
		return
		
	_card_state.execute_card()
	var _hand_index = hand_card_ids.find(_card_id)
	hand_card_ids.pop_at(_hand_index)
	discarded_card_ids.push_back(_card_id)

func debug_hand() -> void:
	DebugUtilities.print_peer_err( str("Player has following cards in hand: ") )		
	for _card_state in hand_card_states:		
		DebugUtilities.print_peer(str(hand_card_states.find(_card_state), ". " ,_card_state.card_data.name))
	pass
	

func shuffle_deck():
	deck_card_ids.shuffle()
