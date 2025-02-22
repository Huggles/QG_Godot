extends DataObject
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
	_init_node()
	return

func _init_node() -> void:	
	node = UnitScene.spawn_unit(self)	
	node.name += "_"+str(id)
	NodeUtilities.units_node.add_child(node, true)	
	node.rotate_y(PI)
	

func set_clickable(callback:Callable):
	clickable_callback = callback
	node.UNIT_CLICKED.connect(		
		func(_unit_scene:UnitScene):			
			UNIT_BECOMES_UNCLICKABLE.emit()			
			if clickable_callback != null:
				clickable_callback.call(self)
			GameManager.game_state.COUNTRY_CLICKED.emit(self)
	)
	UNIT_BECOMES_CLICKABLE.emit() 
	return
	


func can_attack(_faction:Enum.Faction) -> bool:	
	if !is_deployed_to_country: 
		return false
	for connected_country_state:CountryState in country_state.connected_countries(Globals.faction_team_for_faction(_faction)):
		if connected_country_state.occupying_factions.has(_faction):
			return true	
	return false;
