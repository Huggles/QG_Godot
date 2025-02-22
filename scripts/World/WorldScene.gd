extends Node3D


@onready var player_spawn_position = $PlayerSpawnPosition

func _ready() -> void:		
	if get_multiplayer_authority() == multiplayer.get_unique_id():		
		DebugUtilities.print_peer("World Spawned")
		for child in NodeUtilities.players_node.get_children():
			child.position = player_spawn_position.position
		
	
		
	
	
