extends Node

const GAME_MODE_ARG = "game_mode"
const LOCAL_MULTIPLAYER_GAME_ARG = "local_multiplayer_game"

const GAME_LOADING_SCENE_FILE = "res://scenes/loading/game_loading_transition.tscn"

var game_load_transition_screen = preload(GAME_LOADING_SCENE_FILE)
var game_load_transition_screen_instance:Node3D
var game_scene_instance:Node3D

var game_mode:GameMode

# Called when the node enters the scene tree for the first time.
func _ready():
	var args = DebugUtilities.cmd_arguments
	DebugUtilities.print_peer(args)
	if args.has(LOCAL_MULTIPLAYER_GAME_ARG) && args[LOCAL_MULTIPLAYER_GAME_ARG] == true:		
		game_load_transition_screen_instance = game_load_transition_screen.instantiate()		
		game_load_transition_screen_instance.multiplayer_finished_loading.connect(_game_loaded)
		Constants.game_node.add_child(game_load_transition_screen_instance)			

func _game_loaded():
	DebugUtilities.print_peer("game loaded")
	switch_level.rpc()

@rpc("authority", "call_local", "reliable")
func switch_level() -> void:		
	DebugUtilities.print_peer("Switch level")
	if DebugUtilities.cmd_arguments.has(GAME_MODE_ARG):
		var game_mode_arg = DebugUtilities.cmd_arguments.get(GAME_MODE_ARG)
		
		if game_mode_arg == "default":			
			game_mode = GameMode_Default.new()			
			game_mode._start()
			game_scene_instance = game_mode.game_scene_instance
			game_load_transition_screen_instance.get_parent().remove_child(game_load_transition_screen_instance)
			pass
		else:
			DebugUtilities.print_peer("Couldn't determine game mode")
	else:
		DebugUtilities.print_peer("Couldn't determine game mode")
		return
	
	
