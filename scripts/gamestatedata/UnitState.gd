extends StateObject
class_name UnitState

signal BEFORE_UNIT_DEPLOYED_TO_COUNTRY
signal AFTER_UNIT_DEPLOYED_TO_COUNTRY
signal BEFORE_UNIT_REMOVED_FROM_COUNTRY
signal AFTER_UNIT_REMOVED_FROM_COUNTRY


signal UNIT_BECOMES_CLICKABLE
signal UNIT_BECOMES_UNCLICKABLE

var id:int
var type:Enum.UnitType
var faction:String
var faction_enum:Enum.Faction:
	get: return Enum.Faction.get(faction)


var country_id:int = -1
var is_deployed_to_country:bool:
	get: return country_id != null && country_id >= 0
var country_state:CountryState:
	get: return GameManager.game_state.country_state_by_id[country_id]
	
var in_supply:bool = false
var node:UnitScene
var clickable_callback:Callable	

func _init(_type:Enum.UnitType, _faction:String) -> void:			
	self.id = UnitPool.get_unique_unit_id()
	self.type = _type;
	self.faction = _faction;	
	return

func _init_node() -> void:	
	node = UnitScene.spawn_unit(self)	
	node.name += "_"+str(id)
	NodeUtilities.units_node.add_child(node, true)	
	node.rotate_y(PI)
	
func debug() -> void:
	var debug_string:String = str(id);
	debug_string += (" " + str(faction))
	if country_state != null: 
		debug_string += " " + country_state.clabel
	print(debug_string)
	

func set_clickable(callback:Callable):
	clickable_callback = callback
	node.set_clickable(func(_unit_scene:UnitScene):				
			set_unclickable()
			if clickable_callback != null:				
				clickable_callback.call(self)			
	)

func set_unclickable():
	node.set_unclickable()

func set_in_supply():
	self.in_supply = true
	node.hide_out_of_supply()
	
func set_out_of_supply():	
	self.in_supply = false
	node.show_out_of_supply()

func can_attack(_faction:Enum.Faction) -> bool:	
	if !is_deployed_to_country: 
		return false
	for connected_country_state:CountryState in country_state.connected_countries(StaticGameData.faction_team_for_faction(_faction)):
		if connected_country_state.occupying_factions.has(_faction):
			return true	
	return false;

static func for_id(_unit_id:int) -> UnitState:
	return GameManager.game_state.unit_states_by_id[_unit_id]

static func for_ids(_unit_ids:Array[int]) -> Array[UnitState]:
	var response:Array[UnitState] = []
	for _unit_id in _unit_ids:
		response.push_back(for_id(_unit_id))	
	return response
