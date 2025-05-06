
class_name FactionData extends DataObject

var name:String
var index:int
var clabel:String
var color_string:String
var color_string_text:String
var team:String
var homespace:String

var faction_enum:Enum.Faction:
	get: return Enum.Faction.get(name)

var card_front_build_army_texture: String:
	get: return "res://assets/factions/" + name.to_lower() + "/cards/"+ name.to_lower().capitalize().replace(" ", "_") + "_BuildArmy.png"		
		
var card_front_build_navy_texture: String:
	get: return "res://assets/factions/" + name.to_lower() + "/cards/"+ name.to_lower().capitalize().replace(" ", "_") + "_BuildNavy.png"
	
var card_front_land_battle_texture: String:
	get: return "res://assets/factions/" + name.to_lower() + "/cards/"+ name.to_lower().capitalize().replace(" ", "_") + "_LandBattle.png"
	
var card_front_sea_battle_texture: String:
	get: return "res://assets/factions/" + name.to_lower() + "/cards/"+ name.to_lower().capitalize().replace(" ", "_") + "_SeaBattle.png"
	
var card_front_status_texture: String:
	get: return "res://assets/factions/" + name.to_lower() + "/cards/"+ name.to_lower().capitalize().replace(" ", "_") + "_Status.png"
	
var card_front_event_texture: String:
	get: return "res://assets/factions/" + name.to_lower() + "/cards/"+ name.to_lower().capitalize().replace(" ", "_") + "_Event.png"
	
var card_front_ew_texture: String:
	get: return "res://assets/factions/" + name.to_lower() + "/cards/"+ name.to_lower().capitalize().replace(" ", "_") + "_EconomicWarfare.png"
	
var card_front_response_texture: String:
	get: return "res://assets/factions/" + name.to_lower() + "/cards/"+ name.to_lower().capitalize().replace(" ", "_") + "_Response.png"
	
var card_back_texture: String:
	get: return "res://assets/factions/" + name.to_lower() + "/cards/"+ name.to_lower().capitalize().replace(" ", "_") + "_CardBack.png"	

var number_army_units:int
var number_navy_units:int
	
func color()->Color:
	return Color.from_string(color_string, Color.WHITE)

func color_text()->Color:
	return Color.from_string(color_string_text, Color.WHITE)
