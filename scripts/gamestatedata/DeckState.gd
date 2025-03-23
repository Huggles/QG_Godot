class_name DeckState extends StateObject

var faction_state:FactionState
var faction:Enum.Faction:
	get: return faction_state.faction_data.faction_enum
var faction_label:String:
	get: return Enum.Faction.keys()[faction]

var all_card_ids:Array[int]:
	get:
		var _response:Array[int] = []
		_response.append_array(deck_card_ids)
		_response.append_array(hand_card_ids)
		_response.append_array(discarded_card_ids)
		_response.append_array(response_card_ids)
		_response.append_array(status_card_ids)		
		return _response

var deck_card_ids:Array[int]
var deck_card_states:Array[CardState]:
	get: return CardState.for_ids(deck_card_ids)

var hand_card_ids:Array[int]
var hand_card_states:Array[CardState]:
	get: return CardState.for_ids(hand_card_ids)
	
var discarded_card_ids:Array[int]
var discarded_card_states:Array[CardState]:
	get: return CardState.for_ids(discarded_card_ids)
		
var response_card_ids:Array[int]
var response_card_states:Array[CardState]: 
	get: return CardState.for_ids(response_card_ids)

var status_card_ids:Array[int]
var status_card_states:Array[CardState]: 
	get: return CardState.for_ids(status_card_ids)
	
func activatable_status_cards()->Array[CardActivationOption]:
	var _activatable_options:Array[CardActivationOption] = [] 
	for _status_card_state in status_card_states:		
		for _gce in GameManager.game_state.card_play_handler.succesful_change_events:
			if _status_card_state.card_execution_class.can_activate_card(_gce): 
				var _card_activation_option = CardActivationOption.new(_status_card_state.id, "status")
				_card_activation_option.change_event_id = _gce.id
				_activatable_options.push_back(_card_activation_option)		
	return _activatable_options

func activatable_response_cards()->Array[CardActivationOption]:
	var _activatable_options:Array[CardActivationOption] = [] 
	for _response_card_state in response_card_states:		
		for _gce in GameManager.game_state.card_play_handler.succesful_change_events:
			if _response_card_state.card_execution_class.can_activate_card(_gce): 
				var _card_activation_option = CardActivationOption.new(_response_card_state.id, "response")
				_card_activation_option.change_event = _gce.id
				_activatable_options.push_back(_card_activation_option)				
	return _activatable_options

func activatable_cards()->Array[CardActivationOption]:
	var _ac = activatable_status_cards()
	_ac.append_array(activatable_response_cards())
	return _ac

func _init(_faction_state:FactionState) -> void:
	self.faction_state = _faction_state;
	return

func draw_top_card() -> int:	
	if deck_card_ids.size() > 0:
		var _top_card_id = deck_card_ids.pop_front()	
		hand_card_ids.push_back(_top_card_id)
		DebugUtilities.print_peer(str("Drew Card (",faction_label,"): ",CardState.for_id(_top_card_id).card_data.name ) )		
		return _top_card_id
	else:
		DebugUtilities.print_peer_err(str("Deck is empty for: ", Enum.Faction.keys()[faction]) )		
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

func play_card_at_hand_index(_hand_index) -> void:
	if _hand_index >= hand_card_ids.size():
		return
	var _card_id = hand_card_ids[_hand_index]
	play_card(_card_id)

func play_card(_card_id) -> void:
	var _card_state:CardState = CardState.for_id(_card_id)	
	if !_card_state.can_play_card():
		DebugUtilities.print_peer_err(str("Cannot play card: ", _card_state.card_data.clabel ) )		
		return	
	
	if hand_card_ids.has(_card_id):
		play_card_from_hand(_card_id)
	elif discarded_card_ids.has(_card_id):
		play_card_from_discard(_card_id)
	elif deck_card_ids.has(_card_id):
		play_card_from_deck(_card_id)

func activate_card(_activation_option:CardActivationOption):
	if status_card_ids.has(_activation_option.card_id):
		activate_status_card(_activation_option.card_id, _activation_option.change_event_id)
	elif response_card_ids.has(_activation_option.card_id):
		activate_response_card(_activation_option.card_id, _activation_option.change_event_id)

func activate_status_card(_card_id:int, _change_event_id:int) -> void:
	if !status_card_ids.has(_card_id):
		return
	var _card_state:CardState = CardState.for_id(_card_id)	
	_card_state.activate_card(GameChangeEvent.for_id(_change_event_id))
	pass

func activate_response_card(_card_id:int, _change_event_id:int) -> void:
	if !response_card_ids.has(_card_id):
		return
	var _card_state:CardState = CardState.for_id(_card_id)	
	_card_state.activate_card(GameChangeEvent.for_id(_change_event_id))
	pass
	

func play_card_from_deck(_card_id) -> void:
	if deck_card_ids.has(_card_id):
		var _card_state:CardState = CardState.for_id(_card_id)
		_card_state.play_card()
		var _deck_index = deck_card_ids.find(_card_id)
		deck_card_ids.pop_at(_deck_index)
		discarded_card_ids.push_back(_card_id)

func play_card_from_discard(_card_id) -> void:
	if discarded_card_ids.has(_card_id):
		var _card_state:CardState = CardState.for_id(_card_id)
		_card_state.play_card()
	

func play_card_from_hand(_card_id) -> void:
	if hand_card_ids.has(_card_id):		
		var _card_state:CardState = CardState.for_id(_card_id)
		if !self.hand_card_ids.has(_card_id):		
			DebugUtilities.print_peer_err(str("Card not in hand: ", _card_state.card_data.clabel ) )		
			return
		if !_card_state.can_play_card():
			DebugUtilities.print_peer_err(str("Card not play card: ", _card_state.card_data.clabel ) )		
			return
		_card_state.play_card()
		var _hand_index = hand_card_ids.find(_card_id)
		hand_card_ids.pop_at(_hand_index)
		discarded_card_ids.push_back(_card_id)
	
func play_card_by_name(_card_name) -> void:
	for _card_state in CardState.for_ids(all_card_ids):
		if _card_state.card_data.name == _card_name:
			play_card(_card_state.id)
	return
	
func discard_card_at_hand_index(_hand_index) -> void:
	if _hand_index >= hand_card_ids.size():
		return
	var _card_id = hand_card_ids[_hand_index]
	discard_card(_card_id)
	
func discard_card(_card_id) -> void:
	var _card_state:CardState = CardState.for_id(_card_id)
	if !self.hand_card_ids.has(_card_id):		
		DebugUtilities.print_peer_err(str("Card not in hand: ", _card_state.card_data.clabel ) )		
		return	
	var _hand_index = hand_card_ids.find(_card_id)
	hand_card_ids.pop_at(_hand_index)
	discarded_card_ids.push_back(_card_id)

func debug_hand() -> void:
	DebugUtilities.print_peer( str("Player ", faction_label ," has following cards in hand: ") )		
	for _card_state in hand_card_states:		
		DebugUtilities.print_peer(str(hand_card_states.find(_card_state), ". " ,_card_state.card_data.name))
	pass

func debug_status_cards() -> void:
	DebugUtilities.print_peer( str("Player ", faction_label ," has following activatable status cards: ") )		
	for _card_state in status_card_states:		
		DebugUtilities.print_peer(str(status_card_states.find(_card_state), ". " ,_card_state.card_data.name))
	pass

func debug_activatable_status_cards() -> void:
	DebugUtilities.print_peer( str("Faction ", faction_label ," has following activatable status cards: ") )		
	var _activatable_response_options:Array[CardActivationOption] = activatable_status_cards()
	var _counter = 0;
	for _activation_option in _activatable_response_options:
		for _gce in _activation_option.change_events:
			DebugUtilities.print_peer(str(_counter, ". " ,CardState.for_id(_activation_option.card_id).card_data.name, "(", GameChangeEvent.for_id(_gce).display_text, ")"))
	pass	

func debug_activatable_response_cards() -> void:
	DebugUtilities.print_peer( str("Faction ", faction_label ," has following activatable response cards: ") )		
	var _activatable_response_options:Array[CardActivationOption] = activatable_response_cards()
	var _counter = 0;
	for _activation_option in _activatable_response_options:
		for _gce in _activation_option.change_events:
			DebugUtilities.print_peer(str(_counter, ". " ,CardState.for_id(_activation_option.card_id).card_data.name, "(", GameChangeEvent.for_id(_gce).display_text, ")"))
	pass				

func shuffle_deck():
	deck_card_ids.shuffle()
	
static func for_faction(_faction:Enum.Faction) -> DeckState:
	return GameStateUtilities.game_state.faction_state_for_enum(_faction).deck_state
