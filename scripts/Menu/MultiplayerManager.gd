extends Node


signal player_connected(peer_id:int)
signal all_players_connected(players:Array[PlayerScene])
signal player_disconnected(peer_id)
signal server_disconnected

const PORT = 7000
const DEFAULT_SERVER_IP = "127.0.0.1" # IPv4 localhost
const MAX_CONNECTIONS = 6

# This will contain player info for every player,
# with the keys being each player's unique IDs.
var player_scenes = {}

var hosting:bool = false

var max_number_of_players = 1
var number_of_players:
	get: return player_scenes.keys().size()
	
var player_info = {}



# This is the local player info. This should be modified locally
# before the connection is made. It will be passed to every other peer.
# For example, the value of "name" can be set to something the player
# entered in a UI scene.
const player_scene = preload("res://scenes/Player/Player.tscn")
	
func _ready():	
	multiplayer.peer_connected.connect(_on_player_connected)
	multiplayer.peer_disconnected.connect(_on_player_disconnected)
	multiplayer.connected_to_server.connect(_on_connected_ok)
	multiplayer.connection_failed.connect(_on_connected_fail)
	multiplayer.server_disconnected.connect(_on_server_disconnected)
	


func create_game(_max_number_of_players:int, player_name: String) -> bool:
	var peer = ENetMultiplayerPeer.new()
	var error = peer.create_server(PORT, MAX_CONNECTIONS)
	if error:
		print("ERROR HOSTING GAME: ")
		print(error)
		return false
		
	self.max_number_of_players = _max_number_of_players
	multiplayer.multiplayer_peer = peer
	var peer_id = 1
	player_scenes[peer_id] = _instantiate_player_scene(peer_id, player_name)	
	player_connected.emit(peer_id)
	hosting = true
	
	self.player_info = {
		"player_name" : player_name
	}
	
	DebugUtilities.print_peer("Multiplayer Game Created!")
	return true	

func join_game(player_name: String, address = "") -> bool:
	DebugUtilities.print_peer("join_game")
	if address.is_empty():
		address = DEFAULT_SERVER_IP
	var peer = ENetMultiplayerPeer.new()
	var error = peer.create_client(address, PORT)
	if error:		
		print("ERROR JOINING GAME: ")
		print(error)
		return false
	
	self.player_info = {		
		"player_name" : player_name
	}
	
	
	#var peer_id = peer.get_unique_id()
	#my_player_controller = PlayerController.new(peer_id, player_name)
	#players[peer_id] = my_player_controller
	
	multiplayer.multiplayer_peer = peer
	
	return true


func remove_multiplayer_peer():
	multiplayer.multiplayer_peer = null
	
# When a peer connects, send them my player info.
# This allows transfer of all desired data for each player, not only the unique ID.
func _on_player_connected(id):	
	if not hosting and id == 1:
		DebugUtilities.print_peer("Connected to host! Sending my info")	
		_register_player.rpc_id(id, player_info)

@rpc("any_peer", "call_remote", "reliable")
func _register_player(_player_info):		
	DebugUtilities.print_peer("_register_player")
	
	var new_player_id = multiplayer.get_remote_sender_id()	
	DebugUtilities.print_peer(new_player_id)
	DebugUtilities.print_peer(_player_info)
	var pc =  _instantiate_player_scene(new_player_id, _player_info.player_name)	
	player_scenes[new_player_id] = pc	
	
	if number_of_players == max_number_of_players:
		DebugUtilities.print_peer("All Players Connected1")
		all_players_connected.emit(player_scenes.values())
		
	
func _on_player_disconnected(id):	
	DebugUtilities.print_peer("Player disconnected!")
	player_scenes.erase(id)
	player_disconnected.emit(id)


func _on_connected_ok():
	DebugUtilities.print_peer("Player succesfully connected!")


func _on_connected_fail():	
	DebugUtilities.print_peer("Player couldn't connect!")	
	multiplayer.multiplayer_peer = null


func _on_server_disconnected():
	DebugUtilities.print_peer("Server disconnected!")
	multiplayer.multiplayer_peer = null
	player_scenes.clear()
	server_disconnected.emit()	
	
func _instantiate_player_scene(peer_id: int, player_name:String) -> PlayerScene:	
	var my_player_scene = player_scene.instantiate()
	my_player_scene.set_multiplayer_authority(peer_id, true)
	my_player_scene.name = player_name
	my_player_scene.peer_id = peer_id
	my_player_scene.player_name = player_name	
	return my_player_scene
	
