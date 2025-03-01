class_name CardLogicBase extends Object

var peer_id:int
var card_data:CardData
var faction:Enum.Faction:
	get: return card_data.faction
var faction_data:FactionData:
	get: return StaticGameData.faction_data_map[faction];

var player:PlayerScene:
	get:
		return StaticGameData.faction_player_map[faction_data.name]		

var is_played:bool
var is_activated:bool
var is_publicly_visible:bool:
	get: return is_played || (card_data.type == "RESPONSE" && is_activated)

var card_front_texture: Texture2D: 
	get:
		match card_data.type:
			"BUILD_ARMY": return load(faction_data.card_front_build_army_texture)	
			"BUILD_NAVY": return load(faction_data.card_front_build_navy_texture)	
			"LAND_BATTLE": return load(faction_data.card_front_land_battle_texture)	
			"SEA_BATTLE": return load(faction_data.card_front_sea_battle_texture)	
			"STATUS": return load(faction_data.card_front_status_texture)	
			"RESPONSE": return load(faction_data.card_front_response_texture)	
			"EVENT": return load(faction_data.card_front_event_texture)	
			"EW": return load(faction_data.card_front_ew_texture)	
		return 
	
var card_back_texture: Texture2D:
	get: return load(faction_data.card_back_texture)

func _init(_card_data:CardData)->void:	
	card_data = _card_data	

func can_execute_card() -> bool:
	print("CardLogicBase.can_execute_card")
	return true

func execute_card():
	if can_execute_card():
		_start_card()
	else:
		DebugUtilities.print_peer_err(str("Cannot execute card: ", card_data.name))

func _start_card():
	pass

func card_execution_finished():
	GameManager.game_flow.progress_game()	
	pass
	
