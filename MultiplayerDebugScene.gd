extends Node3D

signal multiplayer_finished_loading()

var _instance_number: int
var _player_name: String
var _number_of_players: int

func _ready():	
	var args = DebugUtilities._parse_cmd_arguments()
	_instance_number = int(args["instance"])
	_player_name = args["player_name"]
	if args.has("number_of_players"):
		_number_of_players = int(args["number_of_players"])
		MultiplayerManager.all_players_connected.connect(_all_players_connected)
		MultiplayerManager.create_game(_number_of_players, _player_name)		
	else: 
		await get_tree().create_timer(1.0).timeout				
		MultiplayerManager.join_game(_player_name)		
		
	
func _all_players_connected(players):	
	DebugUtilities.print_peer("All Players Connected:")	
	for player_scene in players:		
		if player_scene:
			Constants.game_node.get_node("Players").add_child(player_scene)	
			
	multiplayer_finished_loading.emit()
