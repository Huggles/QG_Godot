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
	Globals.player_scenes.push_back(self)


func _notification(what):
	if what == NOTIFICATION_PREDELETE:
		print("Player deleted. Name: %s" % player_name)

func _physics_process(_delta):
	_handle_input()

func _handle_input():
	pass
	
func _input(event: InputEvent) -> void:
	if Input.is_key_pressed(KEY_1):
		var country_state:CountryState = Globals.countries_by_name["WESTERN_EUROPE"];
		GameManager.game_state.deploy_unit_to_country(country_state.id, Enum.Faction.GERMANY, Enum.UnitType.ARMY)
	if Input.is_key_pressed(KEY_2):
		var country_state:CountryState = Globals.countries_by_name["WESTERN_EUROPE"];
		if !country_state.is_country_empty:
			var first_faction:Enum.Faction = country_state.units.keys()[0];
			var unit_id:int = country_state.units[first_faction];
			GameManager.game_state.attack_unit(unit_id)
	if Input.is_key_pressed(KEY_3):
		PathFindingService.calculate_path(IPathFindingNode.new(), Enum.Faction.GERMANY,13,53)
	if Input.is_key_pressed(KEY_4):
		var country_state:CountryState = Globals.countries_by_name["ITALY"];
		if country_state.in_range_for_attack(Enum.Faction.GERMANY):
			var first_faction:Enum.Faction = country_state.units.keys()[0];
			var unit_id:int = country_state.units[first_faction];
			GameManager.game_state.attack_unit(unit_id)
		else:
			printerr("CANT ATTACK ITALY")
	if Input.is_key_pressed(KEY_5):
		var country_state:CountryState = Globals.countries_by_name["SIBERIA"];
		if country_state.in_range_for_attack(Enum.Faction.GERMANY):
			var first_faction:Enum.Faction = country_state.units.keys()[0];
			var unit_id:int = country_state.units[first_faction];
			GameManager.game_state.attack_unit(unit_id)
		else:
			printerr("CANT ATTACK SIBERIA")
	
	if Input.is_key_pressed(KEY_6):
		var country_state:CountryState = Globals.countries_by_name["SOUTH_EAST_PACIFIC"];
		GameManager.game_state._set_countries_selectable(
			[country_state],
			Enum.Faction.GERMANY,
			func(country:CountryState):
				print(country.clabel)
		)
		
	if Input.is_key_pressed(KEY_7):		
		GameManager.game_state._set_countries_selectable(
			GameManager.game_state.country_states,
			Enum.Faction.GERMANY, 
			func(country:CountryState):
				print(country.clabel)
		)
	

	
