extends Node3D


@onready var player_spawn_position = $PlayerSpawnPosition

func _ready() -> void:	
	if get_multiplayer_authority() == multiplayer.get_unique_id():		
		DebugUtilities.print_peer("World Spawned")
		
	for child in Constants.players_node.get_children():
		child.position = (Constants.world_node.get_node("PlayerSpawnPosition") as Node3D).position
		
	
	
