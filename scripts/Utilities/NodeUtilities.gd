extends Node

var game_node:Node
var world_node:Node:
	get: return game_node.get_node("World")
var countries_node:Node:
	get: return world_node.get_node("Countries")

var players_node:Node
var units_node:Node




func _ready() -> void:
	game_node = get_tree().root.get_node("Game")
	players_node = game_node.get_node("Players")
	units_node = game_node.get_node("Units")
