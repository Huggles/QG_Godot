extends GameMode
class_name GameMode_Default

const COUNTRY_DATA_PATH = "res://assets/data/QGData_Countries.json"
const FACTIONS_DATA_PATH = "res://assets/data/QGData_Factions.json"
const CARDS_DATA_PATH = "res://assets/data/QGData_Cards.json"
const DECKS_DATA_PATH = "res://assets/data/QGData_Decks.json"	

const GAME_SCENE_FILE = "res://scenes/World/WorldScene.tscn"
var game_scene = preload(GAME_SCENE_FILE)
var game_scene_instance:Node3D

func _spawn_world():
	game_scene_instance = game_scene.instantiate()
	Constants.game_node.add_child(game_scene_instance)

func _init_factions():
	var rows = JSONFileLoader.new(FACTIONS_DATA_PATH).get_json_array()
	for row in rows:
		var faction = FactionData.new(row)
		faction_data.push_back(faction)

func _init_units():
	pass
	
func _init_card_decks():
	var cards = JSONFileLoader.new(CARDS_DATA_PATH).get_json_array()
	for card in cards:
		var carddata = CardData.new(card)
		card_data.push_back(carddata)
	
	var decks = JSONFileLoader.new(DECKS_DATA_PATH).get_json_array()
	for deck in decks:				
		var deckdata = DeckData.new(deck)
		deck_data.push_back(deckdata)	
	
func _init_countries():
	var rows = JSONFileLoader.new(COUNTRY_DATA_PATH).get_json_array()	
	for row in rows:
		var data = CountryData.new(row)
		country_data.push_back(data)
		
func _spawn_units():	
	for faction_d in faction_data:
		for n in faction_d.number_army_units:
			var unit_scene_instance = UnitSceneBase.spawn_unit(faction_data_map[faction_d.name], Enum.UnitType.ARMY)	
			Constants.units_node.add_child(unit_scene_instance, true)
			unit_scene_instance.position = Vector3(100 * n, 0, faction_d.index * 100)
			unit_scene_instance.rotate_y(45)
			all_units.push_back(unit_scene_instance)	
			
		for n in faction_d.number_navy_units:
			var unit_scene_instance = UnitSceneBase.spawn_unit(faction_data_map[faction_d.name], Enum.UnitType.NAVY)	
			Constants.units_node.add_child(unit_scene_instance, true)
			unit_scene_instance.position = Vector3(100 * (n +faction_d.number_army_units), 0, faction_d.index * 100)
			unit_scene_instance.rotate_y(45)
			all_units.push_back(unit_scene_instance)		

func _spawn_countries():
	for country_d in country_data:				
		var country_scene_instance = CountryScene.spawn_country(country_d)
		Constants.countries_node.add_child(country_scene_instance, false )	
		country_scene_instance.position = country_d.WorldPositionCenter
		country_scenes.push_back(country_scene_instance)

func _place_starting_units():	
	pass
	for faction_d in faction_data:
		var unit = units_by_faction.get(faction_d.name)[0]
		country_map[faction_d.homespace].add_unit(unit)
		
