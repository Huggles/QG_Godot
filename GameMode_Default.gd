extends GameMode
class_name GameMode_Default

const WORLD_SCENE_FILE = "res://scenes/World/WorldScene.tscn"
var world_scene = preload(WORLD_SCENE_FILE)


func _spawn_world():
	world_scene_instance = world_scene.instantiate()
	NodeUtilities.network_node.add_child(world_scene_instance)
		
func _spawn_units():
	for faction_d in Globals.faction_data:
		for i in faction_d.number_army_units:
			var unit_state:UnitState = UnitState.new(Enum.UnitType.ARMY,faction_d.name)
			GameManager.game_state.unit_states.push_back(unit_state)
			
		for j in faction_d.number_navy_units:
			var unit_state:UnitState = UnitState.new(Enum.UnitType.NAVY,faction_d.name)
			GameManager.game_state.unit_states.push_back(unit_state)
			

func _spawn_countries():
	for country_d in Globals.country_data:				
		var country_state:CountryState = CountryState.new(country_d)		
		GameManager.game_state.country_states.push_back(country_state)
	for country_state in GameManager.game_state.country_states:
		country_state.init_neighbor_country_state_array()
		

func _place_starting_units():		
	for faction_d in Globals.faction_data:
		var country:CountryState = Globals.countries_by_name[faction_d.homespace];		
		var faction_enum:Enum.Faction = faction_d.faction_enum
		GameManager.game_state.deploy_unit_to_country(country.id, faction_enum, Enum.UnitType.ARMY)
		
		
		
