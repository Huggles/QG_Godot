extends Object
class_name GameMode

signal game_mode_setup_finished()

const COUNTRY_SCENE = "res://scenes/World/Country.tscn"
const COUNTRY_DATA_PATH = "res://assets/data/QGData_Countries.json"
const FACTIONS_DATA_PATH = "res://assets/data/QGData_Factions.json"
const CARDS_DATA_PATH = "res://assets/data/QGData_Cards.json"
const DECKS_DATA_PATH = "res://assets/data/QGData_Decks.json"	

var world_scene_instance:Node3D
	
func _start() -> void:	
	#This Runs on the server only
	_spawn_world()
	
	var data = _load_data()
	
	#Data 
	Globals._init_static_data.rpc(JSON.stringify(data))	
	
	_spawn_countries()
	_spawn_units()	
	_place_starting_units()		
	
func _spawn_world():
	pass
	

func _load_data() -> Dictionary:		
	var faction_data = JSONFileLoader.new(FACTIONS_DATA_PATH).get_json_string()
	var cards_data = JSONFileLoader.new(CARDS_DATA_PATH).get_json_string()
	var deck_data = JSONFileLoader.new(DECKS_DATA_PATH).get_json_string()
	var country_data = JSONFileLoader.new(COUNTRY_DATA_PATH).get_json_string()	
	
	return {
		"faction_data" : faction_data,
		"cards_data" : cards_data,
		"deck_data" : deck_data,
		"country_data" : country_data
	}
		
func _spawn_units():
	pass

func _spawn_countries():
	pass

func _place_starting_units():
	pass
	
