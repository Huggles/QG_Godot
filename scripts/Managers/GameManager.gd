extends Node

const GAME_MODE_ARG = "game_mode"
const LOCAL_MULTIPLAYER_GAME_ARG = "local_multiplayer_game"

const GAME_LOADING_SCENE_FILE_RESOURCE = "res://scenes/loading/game_loading_transition.tscn"
const UI_SCENE_RESOURCE = "res://scenes/userinterface/game_user_interface_base.tscn"
const PLAYER_SCENE_RESOURCE = "res://scenes/Player/Player.tscn"

var ui_screen = preload(UI_SCENE_RESOURCE)
var game_load_transition_screen = preload(GAME_LOADING_SCENE_FILE_RESOURCE)
var player_scene = preload(PLAYER_SCENE_RESOURCE)

var game_load_transition_screen_instance:Node3D
var world_scene_instance:Node3D
var player_instance:Node3D


var game_mode:GameMode
var game_state:GameState
var game_flow:GameFlow
var player_states:Array[PlayerScene] = []

var my_camera:PlayerCamera3D: 
	get: return get_viewport().get_camera_3d()
var my_input_manager:InputManager: 
	get: return player_states[0].input_manager

# Called when the node enters the scene tree for the first time.
func _ready():
	var args = DebugUtilities.cmd_arguments
	DebugUtilities.print_peer(args)
	if args.has(LOCAL_MULTIPLAYER_GAME_ARG) && args[LOCAL_MULTIPLAYER_GAME_ARG] == true:		
		_load_multiplayer_game()
	else:						
		_load_singleplayer_game()
	
func _load_singleplayer_game():
	DebugUtilities.print_peer("Starting single player game")	
	player_instance = player_scene.instantiate()
	NodeUtilities.players_node.add_child(player_instance)
	player_states.push_back(player_instance)
	_setup_game_mode()

func _load_multiplayer_game():
	game_load_transition_screen_instance = game_load_transition_screen.instantiate()		
	game_load_transition_screen_instance.multiplayer_finished_loading.connect(_multiplayer_loaded)
	NodeUtilities.game_node.add_child(game_load_transition_screen_instance)

func _multiplayer_loaded():
	DebugUtilities.print_peer("game loaded")
	_setup_game_mode()	
	
func _setup_game_mode():
	DebugUtilities.print_peer("_setup_game_mode")
	if DebugUtilities.cmd_arguments.has(GAME_MODE_ARG):
		var game_mode_arg = DebugUtilities.cmd_arguments.get(GAME_MODE_ARG)		
		if game_mode_arg == "default":						
			self.game_state = GameState.new()
			self.game_mode = GameMode_Default.new() 
			self.game_mode._start()
			world_scene_instance = game_mode.world_scene_instance
			_switch_level.rpc()
			
			self.game_flow = GameFlow.new()
			self.game_flow.start_game()
			self.game_flow.progress_game()
			pass
		else:
			DebugUtilities.print_peer("Couldn't determine game mode")
	else:
		DebugUtilities.print_peer("Couldn't determine game mode")
		return
	



@rpc("authority", "call_local", "reliable")
func _switch_level() -> void:		
	DebugUtilities.print_peer("Switch level")
	if(game_load_transition_screen_instance != null):
		game_load_transition_screen_instance.get_parent().remove_child(game_load_transition_screen_instance)	
	#_show_ui()

func _show_ui():	
	DebugUtilities.print_peer("Show UI")
	var ui_screen_instantiated = ui_screen.instantiate()	
	ui_screen_instantiated.get_node("FactionHandDisplay").faction = "GERMANY"
	NodeUtilities.user_interface.add_child(ui_screen_instantiated)

	
