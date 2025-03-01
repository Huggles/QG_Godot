extends GameMode
class_name GameMode_Default

const WORLD_SCENE_FILE = "res://scenes/World/WorldScene.tscn"
var world_scene = preload(WORLD_SCENE_FILE)

func _generate_faction_states():
	for _data in StaticGameData.faction_data:
		var _faction_state:FactionState = FactionState.new(_data)		
		GameManager.game_state.faction_states[_faction_state.faction] = _faction_state
		
	var card_counter:int = 0;
	
	for _faction_deck_data:DeckData in StaticGameData.deck_data:		
		var _faction_state:FactionState = GameManager.game_state.faction_states[_faction_deck_data.faction_enum]
		var card_states_for_faction:Array[CardState]
		for card:Dictionary in _faction_deck_data.cards:		
			var card_name = card.get("card_name")
			var card_number = card.get("number")
			for n in card_number:				
				var card_data = StaticGameData.card_data_by_name[card_name];			
				var card_state = CardState.new(card_data)
				card_state.id = card_counter
				GameManager.game_state.card_states.push_back(card_state)
				card_counter+=1
				card_states_for_faction.push_back(card_state)
				_faction_state.deck_card_ids.push_back(card_state.id)
	

func _generate_country_states():
	for _data in StaticGameData.country_data:
		var _country_state:CountryState = CountryState.new(_data)		
		GameManager.game_state.country_states.push_back(_country_state)	
	
	for _country_state in GameManager.game_state.country_states:
		_country_state.init_neighbor_country_state_array()

func _generate_unit_states():
	for _faction_data:FactionData in StaticGameData.faction_data:		
		for i in _faction_data.number_army_units:
				var unit_state:UnitState = UnitState.new(Enum.UnitType.ARMY,_faction_data.name)
				GameManager.game_state.unit_states.push_back(unit_state)
				
		for j in _faction_data.number_navy_units:
			var unit_state:UnitState = UnitState.new(Enum.UnitType.NAVY,_faction_data.name)
			GameManager.game_state.unit_states.push_back(unit_state)

func _spawn_world():
	world_scene_instance = world_scene.instantiate()
	NodeUtilities.network_node.add_child(world_scene_instance)
		
func _spawn_units():		
	for _unit_state:UnitState in GameManager.game_state.unit_states:
		_unit_state._init_node()

func _spawn_countries():
	for _country_state:CountryState in GameManager.game_state.country_states:
		_country_state._init_node()

func _place_starting_units():			
	for _faction_data in StaticGameData.faction_data:
		var _country_state:CountryState = StaticGameData.countries_by_name[_faction_data.homespace];					
		var _deploy_unit_change_event:DeployUnitChangeEvent = DeployUnitChangeEvent.new(_faction_data.faction_enum, _country_state.id, Enum.DeployType.BUILD)			
		_deploy_unit_change_event.apply_change()					
	
	var _country_state:CountryState = StaticGameData.countries_by_name["SIBERIA"]
	var _deploy_unit_change_event:DeployUnitChangeEvent = DeployUnitChangeEvent.new(Enum.Faction.GERMANY, _country_state.id, Enum.DeployType.BUILD)			
	_deploy_unit_change_event.apply_change()					
	
	_country_state = StaticGameData.countries_by_name["CHINA"]			
	_deploy_unit_change_event = DeployUnitChangeEvent.new(Enum.Faction.GERMANY, _country_state.id, Enum.DeployType.BUILD)			
	_deploy_unit_change_event.apply_change()		


		
		
	
		
		
		
