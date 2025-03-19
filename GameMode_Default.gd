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
				card_state.faction = _faction_state.faction
				GameManager.game_state.card_states.push_back(card_state)
				card_counter+=1
				card_states_for_faction.push_back(card_state)				
				_faction_state.deck_state.deck_card_ids.push_back(card_state.id)
	

func _generate_country_states():
	#First create all country states
	for _data in StaticGameData.country_data:
		var _country_state:CountryState = CountryState.new(_data)		
		GameManager.game_state.country_states.push_back(_country_state)	
	
	#Then find all potential neighbors
	for _country_state in GameManager.game_state.country_states:
		_country_state.init_neighbor_country_state_array()
	
	#Create Straights
	for _country_data in StaticGameData.country_data:
		var _country_state:CountryState = CountryState.for_name(_country_data.name)
		var _straight_state:StraightState = StraightState.new(_country_state.id, _country_data.straight_data)
		_country_state.straight_state = _straight_state
		GameManager.game_state.straight_states.push_back(_straight_state)

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
	var _country_state:CountryState
	var _deploy_unit_change_event:DeployUnitChangeEvent
	
	for _faction_data in StaticGameData.faction_data:
		_country_state = StaticGameData.countries_by_name[_faction_data.homespace]
		_deploy_unit_change_event = DeployUnitChangeEvent.new(_faction_data.faction_enum, _country_state.id, Enum.DeployType.RECRUIT)
		ChangeEventHandler.execute_change_event(_deploy_unit_change_event)							
	
	_country_state = StaticGameData.countries_by_name["SIBERIA"]
	_deploy_unit_change_event = DeployUnitChangeEvent.new(Enum.Faction.GERMANY, _country_state.id, Enum.DeployType.RECRUIT)			
	ChangeEventHandler.execute_change_event(_deploy_unit_change_event)
	
	_country_state = StaticGameData.countries_by_name["BALKANS"]			
	_deploy_unit_change_event = DeployUnitChangeEvent.new(Enum.Faction.GERMANY, _country_state.id, Enum.DeployType.RECRUIT)			
	ChangeEventHandler.execute_change_event(_deploy_unit_change_event)
	
	_country_state = StaticGameData.countries_by_name["MEDITERRANEAN_SEA"]			
	_deploy_unit_change_event = DeployUnitChangeEvent.new(Enum.Faction.GERMANY, _country_state.id, Enum.DeployType.RECRUIT)			
	ChangeEventHandler.execute_change_event(_deploy_unit_change_event)

	_country_state = StaticGameData.countries_by_name["NORTH_AFRICA"]			
	_deploy_unit_change_event = DeployUnitChangeEvent.new(Enum.Faction.GERMANY, _country_state.id, Enum.DeployType.RECRUIT)			
	ChangeEventHandler.execute_change_event(_deploy_unit_change_event)	

	_country_state = StaticGameData.countries_by_name["NORTH_SEA"]
	_deploy_unit_change_event = DeployUnitChangeEvent.new(Enum.Faction.UNITED_KINGDOM, _country_state.id, Enum.DeployType.RECRUIT)			
	ChangeEventHandler.execute_change_event(_deploy_unit_change_event)

	_country_state = StaticGameData.countries_by_name["UKRAINE"]			
	_deploy_unit_change_event = DeployUnitChangeEvent.new(Enum.Faction.SOVIET, _country_state.id, Enum.DeployType.RECRUIT)			
	ChangeEventHandler.execute_change_event(_deploy_unit_change_event)
	
	_country_state = StaticGameData.countries_by_name["EAST_PACIFIC"]			
	_deploy_unit_change_event = DeployUnitChangeEvent.new(Enum.Faction.UNITED_STATES, _country_state.id, Enum.DeployType.RECRUIT)			
	ChangeEventHandler.execute_change_event(_deploy_unit_change_event)
	
	_country_state = StaticGameData.countries_by_name["CENTRAL_PACIFIC"]			
	_deploy_unit_change_event = DeployUnitChangeEvent.new(Enum.Faction.UNITED_STATES, _country_state.id, Enum.DeployType.RECRUIT)			
	ChangeEventHandler.execute_change_event(_deploy_unit_change_event)
	
	var _deck_state_germany = DeckState.for_faction(Enum.Faction.GERMANY)
	_deck_state_germany.play_card_by_name("Status_Blitzkrieg",func():)
	_deck_state_germany.play_card_by_name("Status_SyntheticFuel",func():)

	
	
	
	


		
		
	
		
		
		
