class_name GameFlow

var game_started:bool = false
var game_turn:int = 0;
var turn_step:int = 0;


var round:int:	
	get:
		@warning_ignore("integer_division") 
		return ((game_turn-1) / Enum.Faction.keys().size())+1

var current_faction:Enum.Faction:
	get: 
		var faction_int = ((game_turn-1) % Enum.Faction.keys().size())				
		@warning_ignore("integer_division")
		return faction_int if game_turn > 0 else Enum.Faction.GERMANY
		
var current_faction_state:FactionState:
	get: return game_state.faction_state_for_enum(current_faction)
var current_faction_deck_state:DeckState:
	get: return DeckState.for_faction(current_faction)
	
var current_faction_team:Enum.FactionTeam:
	get: return Enum.FactionTeam.ALLIES if game_turn > 0 && game_turn % 2 == 0 else Enum.FactionTeam.AXIS
	
var turn_step_methods:Array[Callable] = [_start_turn_step, _play_card_step, _supply_step, _victory_point_step, _discard_step, _draw_step, _start_new_turn]

var vp_step_handler:VictoryPointStepHandlerDefault = VictoryPointStepHandlerDefault.new()

var game_state:GameState:
	get: return GameManager.game_state

var victory_point_summaries:Dictionary = {}

func _init() -> void:
	pass

func _reset_card_hanlder():
	if GameManager.game_state.card_play_handler != null:
		GameManager.game_state.card_play_handler.disable()
	GameManager.game_state.card_play_handler = CardPlayHandler.new();	
	
func start_game() -> void:
	for _faction:FactionState in game_state.faction_states.values():
		_faction.deck_state.draw_cards(7)		
	EventBusLocal.recalculate_supply.emit()
	for _faction:Enum.Faction in Enum.Faction.values():
		victory_point_summaries[_faction] = []
	game_started = true
	_start_new_turn()

func progress_game() -> void:
	print(str("progress_game: ", game_turn))
	if GameManager.game_state.card_play_handler != null:
		GameManager.game_state.card_play_handler.disable()
	GameManager.game_state.card_play_handler = CardPlayHandler.new();	
	_start_next_step()

func _start_next_step() -> void:
	print("_start_next_step")
	turn_step += 1;	
	turn_step_methods[turn_step - 1].call()
	EventBusLocal.next_step_started.emit(turn_step)
	
func _start_new_turn() -> void:
	print("_start_new_turn")
	game_turn += 1;
	turn_step = 0;
	print(str("Game turn: ", game_turn, " ( ", Enum.Faction.keys()[current_faction] ," / ", Enum.FactionTeam.keys()[current_faction_team], " )"))
	EventBusLocal.new_turn_started.emit(game_turn)
	_start_next_step()

func _start_turn_step() -> void:	
	print("_start_turn_step")	
	progress_game()
	pass

func _play_card_step() -> void:	
	print("_play_card_step")
	GameManager.game_state.card_play_handler.request_card_play()
	await EventBusLocal.card_play_handler_completed	
	progress_game()
	
func _supply_step() -> void:
	print("_supply_step")
	EventBusLocal.recalculate_supply.emit()
	for _unit_id in GameStateUtilities.unsupplied_unit_ids(current_faction):
		var _unit_out_of_supply_event:UnitOutOfSupplyEvent = UnitOutOfSupplyEvent.new(_unit_id)
		CardPlayHandler.instance.execute_change_event(_unit_out_of_supply_event, true)
		await EventBusLocal.card_play_handler_completed
		_reset_card_hanlder()	
	progress_game()
	pass

func _victory_point_step() -> void:
	print("_victory_point_step")
	vp_step_handler.process_turn(current_faction)
	progress_game()
	pass
	
func _discard_step() -> void:
	print("_discard_step")
	progress_game()
	#GameManager.player_states[0].input_manager.set_discard_input_active()
	pass

func _draw_step() -> void:
	print("_draw_step")	
	current_faction_deck_state.debug_hand()
	current_faction_deck_state.draw_cards(7 - current_faction_deck_state.hand_card_ids.size())
	current_faction_deck_state.debug_hand()
	progress_game()
	pass
	
