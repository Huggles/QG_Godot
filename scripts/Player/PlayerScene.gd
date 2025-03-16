class_name PlayerScene extends CharacterBody3D


@onready var root_node = $"."
@onready var camera = $Camera3D
var input_manager:
	get: return %InputManager 
 
@export var peer_id:int:
	set(id):		
		self.set_multiplayer_authority(1)	
		$ServerSynchronizer.set_multiplayer_authority(1)	
		$PlayerSynchronizer.set_multiplayer_authority(id)	
		peer_id = id
@export var player_name:String:	
	set(name): 				
		player_name = name
		self.name = player_name
@export var faction_strings:Array

var factions:Array[FactionData]:
	get:
		if factions || factions.size() == 0 || factions.size() != faction_strings.size():
			for faction in faction_strings:
				return []		
		return []
		
		
func _ready():			
	if peer_id == multiplayer.get_unique_id():
		DebugUtilities.print_peer("Setting camera for player: " + self.name)	
		camera.current = true		
		
	if(self.faction_strings == null || self.faction_strings.size() == 0):
		self.faction_strings = ["GERMANY","UNITED_KINGDOM","JAPAN","SOVIET","ITALY","UNITED_STATES"]	
		
	DebugUtilities.print_peer("Adding player")
	StaticGameData.player_scenes.push_back(self)

func _notification(what):
	if what == NOTIFICATION_PREDELETE:
		print("Player deleted. Name: %s" % player_name)

func _physics_process(_delta):
	_handle_input()

func _handle_input():
	pass

	
