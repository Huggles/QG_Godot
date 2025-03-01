class_name GameFlow

var game_started:bool = false
var game_turn:int = 0;
var turn_step:int = 0;
var current_faction:Enum.Faction:
	get: 
		var faction_int = ((game_turn-1) % Enum.Faction.keys().size())				
		return faction_int if game_turn > 0 else Enum.Faction.GERMANY
		
var current_faction_state:FactionState:
	get: return game_state.faction_state_for_enum(current_faction)
	
var current_faction_team:Enum.FactionTeam:
	get: return Enum.FactionTeam.ALLIES if game_turn > 0 && game_turn % 2 == 0 else Enum.FactionTeam.AXIS
	
var turn_step_methods:Array[Callable] = [_start_turn_step, _play_card_step, _supply_step, _discard_step, _draw_step, _start_new_turn]

var game_state:GameState:
	get: return GameManager.game_state
	
signal NEW_TURN_STARTED
signal NEXT_STEP_STARTED

signal CHANGE_EVENT_ADDED

func _init() -> void:
	pass
	
func start_game() -> void:
	for _faction:FactionState in game_state.faction_states.values():
		_faction.draw_cards(7)		
	GameStateUtilities.recalculate_supply()
	game_started = true

func progress_game() -> void:
	if game_turn == 0:
		_start_new_turn()
	_start_next_step()
	
func _start_new_turn() -> void:
	game_turn += 1;
	turn_step = 0;
	print(str("Game turn: ", game_turn, " ( ", Enum.Faction.keys()[current_faction] ," / ", Enum.FactionTeam.keys()[current_faction_team], " )"))
	NEW_TURN_STARTED.emit(game_turn)
	_start_next_step()
	pass

func _start_next_step() -> void:
	turn_step += 1;	
	turn_step_methods[turn_step - 1].call()
	NEXT_STEP_STARTED.emit(turn_step)	

func _start_turn_step() -> void:	
	print("_start_turn_step")
	pass

func _play_card_step() -> void:	
	print("_play_card_step")
	GameManager.player_states[0].input_manager.set_card_input_active()
	current_faction_state.debug_hand()
	pass
	
func _supply_step() -> void:
	print("_supply_step")
	pass
	
func _discard_step() -> void:
	print("_discard_step")
	pass

func _draw_step() -> void:
	print("_draw_step")
	pass
	
