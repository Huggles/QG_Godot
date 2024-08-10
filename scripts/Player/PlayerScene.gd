class_name PlayerScene
extends CharacterBody3D

const MOVEMENT_SPEED = 3000.0
const ZOOM_SPEED = 7500.0

@onready var root_node = $"."
@onready var player_synchronizer = $PlayerSynchronizer
@onready var camera = $Camera3D

 
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
	
var factions:Array[FactionData]

func _ready():		
	if peer_id == multiplayer.get_unique_id():
		DebugUtilities.print_peer("Setting camera for player: " + self.name)	
		camera.current = true		


func _notification(what):
	if what == NOTIFICATION_PREDELETE:
		print("Player deleted. Name: %s" % player_name)

func _physics_process(_delta):
	_handle_input()

func _handle_input():
	pass
	
	

	
