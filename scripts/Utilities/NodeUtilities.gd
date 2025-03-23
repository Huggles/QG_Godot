extends Node

var game_node:Node

var world_node:Node:
	get: return network_node.get_node("World") if network_node != null else null
var countries_node:Node:
	get: return world_node.get_node("Countries") if world_node != null else null

var players_node:Node
var units_node:Node
var network_node:Node3D
var user_interface:CanvasLayer

func _ready() -> void:
	game_node = get_tree().root.get_node("Game")
	players_node = game_node.get_node("Players")
	units_node = game_node.get_node("Units")
	network_node = game_node.get_node("Networked")
	user_interface = game_node.get_node("UserInterface")
