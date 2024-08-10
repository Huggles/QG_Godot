extends Node

const LOBBY_SCENE = "res://scenes/MultiplayerLobby.tscn"
@onready var MAIN_MENU_NODE = $CanvasLayer/MainMenu
func _ready():				
	MAIN_MENU_NODE.host_button_clicked.connect(_on_host_button_pressed)
	MAIN_MENU_NODE.join_button_clicked.connect(_on_multiplayer_button_pressed)
	MAIN_MENU_NODE.exit_button_clicked.connect(_on_exit_game_button_pressed)

func _on_host_button_pressed():
	MultiplayerManager.create_game(2, "Host")		
	#_navigate_to_lobby()


func _on_multiplayer_button_pressed():
	#MultiplayerManager.join_game()
	var packed_scene = load("res://scenes/World/WorldScene.tscn")
	get_tree().change_scene_to_packed(packed_scene)
	
	
	#_navigate_to_lobby()
	
func _navigate_to_lobby():
	var scene = preload(LOBBY_SCENE)
	var node  = scene.instantiate()
	add_child(node)


func _on_exit_game_button_pressed():
	get_tree().quit()
