extends Object
class_name CardBase


var peer_id:int
var card_data:CardData
var faction:FactionData
var player:PlayerScene:
	get:
		return Globals.faction_player_map[faction.name]		

var is_played:bool
var is_activated:bool
var is_publicly_visible:bool:
	get: return is_played || (card_data.type == "RESPONSE" && is_activated)

var card_front_texture: Texture2D: 
	get:
		match card_data.type:
			"BUILD_ARMY": return load(faction.card_front_build_army_texture)	
			"BUILD_NAVY": return load(faction.card_front_build_navy_texture)	
			"LAND_BATTLE": return load(faction.card_front_land_battle_texture)	
			"SEA_BATTLE": return load(faction.card_front_sea_battle_texture)	
			"STATUS": return load(faction.card_front_status_texture)	
			"RESPONSE": return load(faction.card_front_response_texture)	
			"EVENT": return load(faction.card_front_event_texture)	
			"EW": return load(faction.card_front_ew_texture)	
		return 
	
var card_back_texture: Texture2D:
	get: return load(faction.card_back_texture)


func _init(_card_data:CardData, _faction_data:FactionData)->void:	
	card_data = _card_data
	faction = _faction_data

func execute_card()->bool:
	return false
