class_name CardPlayHandler extends Object


#Dictionay of NextSteps
var available_next_steps_per_faction:Dictionary = {}

var change_events:Array[GameChangeEvent]
var succesful_change_events:Array[GameChangeEvent]
var last_activating_team:Enum.FactionTeam

var card_id:int
var card_state:CardState:
	get: return CardState.for_id(card_id)

signal card_play_finished

var request_order:Array:
	get:
		var order = [] 
		order.append_array(StaticGameData.opponent_factions_for_team(last_activating_team))
		order.append_array(StaticGameData.factions_for_team(last_activating_team))         
		return order

func handle_change_event(_gce_id):
	succesful_change_events.push_back( GameChangeEvent.for_id(_gce_id))
	
func request_card():
	self._calculate_next_steps()

	GameManager.player_states[0].input_manager.set_play_card_input_active()
	GameManager.game_flow.current_faction_deck_state.debug_hand()    
	var _activation_option:CardActivationOption = await EventBusLocal.card_selected
	if _activation_option.card_id > -1:
		self.card_id = _activation_option.card_id		
		if card_state.can_play_card():
			InputMessageLabel.hide_node()
			PlayerActionLabel.hide_node()
			
			if _activation_option.is_before:
				card_state.card_execution_class.activate_before(GameChangeEvent.for_id(_activation_option.change_event_id))
			else:
				card_state.card_execution_class.activate_card(GameChangeEvent.for_id(_activation_option.change_event_id))
			handle_card_play()
	else:
		EventBusLocal.card_play_handler_completed.emit(self)

func handle_card_play():
	DeckState.for_faction(card_state.faction).play_card(card_state.id)
	while card_state.card_execution_class.has_next_action():
		await card_state.card_execution_class.play_next_action()
		
	EventBusLocal.card_play_handler_completed.emit(self)

func try_play_card(_card_id:int):
	var _card_state:CardState = CardState.for_id(_card_id)
	if _card_state.can_play_card():
		GameManager.my_input_manager.set_no_input_active()
		InputMessageLabel.hide_node()
		DeckState.for_faction(_card_state.faction).play_card(_card_id)        
	pass
	
func _execute_card():
	pass


func _calculate_next_steps() -> void:
	for _faction:Enum.Faction in Enum.Faction.values():
		var _next_steps:Array[NextStep] = self._calculate_next_steps_for_faction(_faction)
	

func _calculate_next_steps_for_faction(_faction:Enum.Faction) -> Array[NextStep]:
	var _next_steps:Array[NextStep] = []
	var _index = 0;

	for _card_state:CardState in DeckState.for_faction(GameManager.game_flow.current_faction).hand_card_states:
		var _next_step:NextStep
		if _card_state.can_play_card():
			_next_step = NextStep.new(str(_index, " - ", _card_state.card_data.name), _index, _faction)			
		else:
			_next_step = NextStep.new(str("[color=red]",_index, " - ", _card_state.card_data.name, "[/color]"), _index, _faction)			
		_index +=1
	return _next_steps

class NextStep:
	enum Type { PlayCard }

	var label:String
	var index:int
	var faction:Enum.Faction

	func _init(_label:String, _index:int, _faction:Enum.Faction) -> void:
		self.label = _label
		self.index = _index
		self.faction = _faction
