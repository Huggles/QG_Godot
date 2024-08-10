extends Object
class_name GameMode

signal game_mode_setup_finished()

const COUNTRY_SCENE = "res://scenes/World/Country.tscn"

##Faction Data
var faction_data:Array[FactionData]
var _faction_data_map:Dictionary
var faction_data_map:Dictionary:
	get: 
		if not _faction_data_map or _faction_data_map.size() == 0: 
			for faction_d in faction_data:
				_faction_data_map[faction_d.name] = faction_d
		return _faction_data_map


##Card Data
var card_data:Array[CardData]
var _card_data_map:Dictionary
var card_data_map:Dictionary:
	get: 
		if not _card_data_map or _card_data_map.size() == 0: 
			for card_d in card_data:
				_card_data_map[card_d.name] = card_d
		return _card_data_map


##Deck Data
var deck_data:Array[DeckData]
var deck_data_map:Dictionary:
	get: 
		if not deck_data_map or deck_data_map.size() == 0: 
			for deck_d in deck_data:
				deck_data_map[deck_d.faction] = deck_d
		return deck_data_map

##Countries
var country_data:Array[CountryData]
var country_scenes:Array[CountryScene]

var _country_map:Dictionary
var country_map:Dictionary:
	get: 
		if not _country_map or _country_map.size() == 0: 
			for country_scene in country_scenes:
				_country_map[country_scene.country_data.name] = country_scene
		return _country_map

##Unit
var all_units:Array[UnitSceneBase]
var units_by_faction:Dictionary:
	get: 
		if not units_by_faction or units_by_faction.size() == 0: 
			for unit in all_units:
				var faction_name = unit.faction.name
				if not units_by_faction.has(faction_name):
					units_by_faction[faction_name] = []
				units_by_faction[faction_name].push_back(unit)
		return units_by_faction
	
func _start():
	_spawn_world()
	
	#Data 
	_init_factions()
	_init_countries()	
	_init_units()
	_init_card_decks()
	
	_spawn_countries()
	_spawn_units()
	
	_place_starting_units()
		
	game_mode_setup_finished.emit()
	
func _spawn_world():
	pass

func _init_factions():
	pass

func _init_units():
	pass
	
func _init_card_decks():
	pass	
	
func _init_countries():
	pass
		
func _spawn_units():
	pass

func _spawn_countries():
	pass

func _place_starting_units():
	pass
	
