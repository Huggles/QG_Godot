extends Node

func _init_static_data(data:String):
	var data_map:Dictionary = JSON.parse_string(data)	
	DebugUtilities.print_peer("Init factions constants")
	
	var faction_rows = JSON.parse_string(data_map.faction_data)
	var card_rows = JSON.parse_string(data_map.cards_data)
	var deck_rows = JSON.parse_string(data_map.deck_data)
	var country_rows = JSON.parse_string(data_map.country_data)
	
	for faction_row in faction_rows:
		var faction = FactionData.new(faction_row)
		faction_data.push_back(faction)
	DebugUtilities.print_peer(str("Loaded ", faction_data.size(), " factions"))
	
	for card_row in card_rows:
		var carddata = CardData.new(card_row)
		card_data.push_back(carddata)	
	DebugUtilities.print_peer(str("Loaded ", card_data.size(), " cards"))
	
	for deck_row in deck_rows:				
		var deckdata = DeckData.new(deck_row)
		deck_data.push_back(deckdata)	
	DebugUtilities.print_peer(str("Loaded ", deck_data.size(), " decks"))
		
	for country_row in country_rows:
		var countrydata = CountryData.new(country_row)
		country_data.push_back(countrydata)
	DebugUtilities.print_peer(str("Loaded ", country_data.size(), " countries"))

var player_scenes:Array[PlayerScene] = []
var player_scenes_map:Dictionary:
	get: 
		if player_scenes_map || player_scenes_map.size() == 0 || player_scenes_map.size() != player_scenes.size():
			for player_scene in player_scenes:
				player_scenes_map[player_scene.peer_id] = player_scene
		return player_scenes_map

var faction_player_map:Dictionary:
	get: 
		if faction_player_map || faction_player_map.size() == 0 || faction_player_map.size() != player_scenes.size():
			for player_scene in player_scenes:				
				for faction in player_scene.faction_strings:					
					faction_player_map[faction] = player_scene
		return faction_player_map
		
##Faction Data
var faction_data:Array[FactionData]
var faction_data_map:Dictionary:
	get: 
		if not faction_data_map or faction_data_map.size() == 0 or faction_data_map.size() != faction_data.size(): 
			for faction_d in faction_data:
				faction_data_map[faction_d.name] = faction_d
		return faction_data_map


##Card Data
var card_data:Array[CardData]
var card_data_by_name:Dictionary:
	get: 
		if not card_data_by_name or card_data_by_name.size() == 0: 
			for card_d in card_data:
				card_data_by_name[card_d.name] = card_d
		return card_data_by_name


##Deck Data
var deck_data:Array[DeckData]
var deck_data_map:Dictionary:
	get: 
		if not deck_data_map or deck_data_map.size() == 0: 
			for deck_d in deck_data:
				deck_data_map[deck_d.faction_enum] = deck_d
		return deck_data_map

##Countries
var country_data:Array[CountryData]
var countries_by_name:Dictionary:
	get: 
		if not countries_by_name or countries_by_name.size() == 0: 
			for country_state in GameManager.game_state.country_states:
				countries_by_name[country_state.name] = country_state
		return countries_by_name
var countries_by_id:Dictionary:
	get: 
		if not countries_by_id or countries_by_id.size() == 0: 
			for country_state in GameManager.game_state.country_states:
				countries_by_id[country_state.id] = country_state
		return countries_by_id

##Unit
var unit_states_by_faction_enum:Dictionary:
	get: 
		if not unit_states_by_faction_enum or unit_states_by_faction_enum.size() == 0: 
			for unit in GameManager.game_state.unit_states:			
				var unit_faction:Enum.Faction = unit.faction_enum	
				if not unit_states_by_faction_enum.has(unit_faction):
					unit_states_by_faction_enum[unit_faction] = []
				unit_states_by_faction_enum[unit_faction].push_back(unit)
		return unit_states_by_faction_enum
		

func faction_team_for_faction(_faction:Enum.Faction) -> Enum.FactionTeam:
	if( _faction == Enum.Faction.GERMANY || _faction == Enum.Faction.JAPAN || _faction == Enum.Faction.ITALY ):
		return Enum.FactionTeam.AXIS
	if( _faction == Enum.Faction.UNITED_KINGDOM || _faction == Enum.Faction.SOVIET || _faction == Enum.Faction.UNITED_STATES ):
		return Enum.FactionTeam.ALLIES
	return Enum.FactionTeam.NONE
	
func opponent_faction_team_for_faction(_faction:Enum.Faction) -> Enum.FactionTeam:
	if( _faction == Enum.Faction.UNITED_KINGDOM || _faction == Enum.Faction.SOVIET || _faction == Enum.Faction.UNITED_STATES ):	
		return Enum.FactionTeam.AXIS
	if( _faction == Enum.Faction.GERMANY || _faction == Enum.Faction.JAPAN || _faction == Enum.Faction.ITALY ):
		return Enum.FactionTeam.ALLIES
	return Enum.FactionTeam.NONE

func factions_for_team(_faction_team:Enum.FactionTeam) -> Array[Enum.Faction]:
	if _faction_team == Enum.FactionTeam.AXIS:
		return [Enum.Faction.GERMANY, Enum.Faction.JAPAN, Enum.Faction.ITALY ]
	if _faction_team == Enum.FactionTeam.ALLIES:
		return [Enum.Faction.UNITED_KINGDOM, Enum.Faction.SOVIET, Enum.Faction.UNITED_STATES ]
		
	return []

func opponent_factions_for_team(_faction_team:Enum.FactionTeam) -> Array[Enum.Faction]:
	if _faction_team == Enum.FactionTeam.ALLIES:
		return [Enum.Faction.GERMANY, Enum.Faction.JAPAN, Enum.Faction.ITALY ]
	if _faction_team == Enum.FactionTeam.AXIS:
		return [Enum.Faction.UNITED_KINGDOM, Enum.Faction.SOVIET, Enum.Faction.UNITED_STATES ]
		
	return []
		
