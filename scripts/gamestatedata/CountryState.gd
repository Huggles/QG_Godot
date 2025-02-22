extends DataObject
class_name CountryState

signal COUNTRY_BECOMES_CLICKABLE
signal COUNTRY_BECOMES_UNCLICKABLE

var static_country_data:CountryData

var id:int
var name:String
var name_camel_case:String
var clabel:String
var type:Enum.CountryType
var is_supply:bool
var is_harbor:bool
var harbor1:String
var harbor2:String
var neighbors = []
var neighbor_country_states:Array[CountryState] = []	
		
var clickable_callback:Callable	
func set_clickable(callback:Callable):
	clickable_callback = callback
	node.COUNTRY_CLICKED.connect(		
		func(country_scene:CountryScene):			
			COUNTRY_BECOMES_UNCLICKABLE.emit()			
			if clickable_callback != null:
				clickable_callback.call(self)
			GameManager.game_state.COUNTRY_CLICKED.emit(self)
	)
	COUNTRY_BECOMES_CLICKABLE.emit() 
	return

#Map of FactionEnum -> Unit Id
var units:Dictionary = {}
var is_country_empty:bool:
	get: return self.units.size() == 0
var is_country_full:bool:
	get: return self.units.size() == 3
var occupying_factions:Array[Enum.Faction]:
	get:
		var of:Array[Enum.Faction]
		of.assign(units.keys())
		return of

var occupied_by_team:Enum.FactionTeam:
	get: 
		if occupying_factions.size() == 0:
			return Enum.FactionTeam.NONE
		else:
			return Globals.faction_team_for_faction(occupying_factions[0])
			
var node:CountryScene

func _init(_country_data:CountryData) -> void:
	self.static_country_data = _country_data
	self.id = 				_country_data.number
	self.name = 				_country_data.name
	self.name_camel_case = 	_country_data.name_camel_case
	self.clabel = 			_country_data.clabel	
	self.type = 				_country_data.type
	self.is_supply = 		true if _country_data.is_supply == "true" else false
	self.is_harbor = 		true if _country_data.is_harbor == "true" else false
	self.harbor1 = (_country_data.harbor1 if _country_data.harbor1 != null else "")
	self.harbor2 = (_country_data.harbor2 if _country_data.harbor2 != null else "")
	self.neighbors = 		_country_data.neighbors			
	_init_node();
	
func init_neighbor_country_state_array() -> void:
	for neighbor in neighbors:
		if GameManager.game_state.country_state_by_name.has(neighbor):
			var neighbor_country_state:CountryState = GameManager.game_state.country_state_by_name[neighbor]
			neighbor_country_states.push_back(neighbor_country_state)
		else:
			printerr(str("Couldnt find: ", neighbor, " as neighbor of ", self.name))
		

func _init_node() -> void:
	node = CountryScene.spawn_country(self)
	NodeUtilities.countries_node.add_child(node, false )	
	node.position = static_country_data.WorldPositionCenter	
	
func connected_countries(_faction_team:Enum.FactionTeam) -> Array[CountryState]:
	var cc:Array[CountryState] = [];
	for neighbor_country_state in neighbor_country_states:
		if(self.type == Enum.CountryType.LAND && neighbor_country_state.type == Enum.CountryType.LAND):
			cc.push_back(neighbor_country_state)
		if(self.type == Enum.CountryType.SEA && neighbor_country_state.type == Enum.CountryType.LAND):
			cc.push_back(neighbor_country_state)
		if(self.type == Enum.CountryType.LAND && neighbor_country_state.type == Enum.CountryType.SEA):
			cc.push_back(neighbor_country_state)
		if(self.type == Enum.CountryType.SEA && neighbor_country_state.type == Enum.CountryType.SEA):
			for country_state in GameManager.game_state.country_states:
				if country_state.is_harbor_for(self, neighbor_country_state):
					if country_state.is_occupied_by_team(_faction_team):
						cc.push_back(neighbor_country_state)
					else:
						break;		
	return cc;
	

func can_build(_faction:Enum.Faction) -> bool:
	if (occupied_by_team == Enum.FactionTeam.NONE || occupied_by_team == Globals.faction_team_for_faction(_faction)) && !occupying_factions.has(_faction):
		return true;
	return false;
	
func in_range_for_attack(_faction:Enum.Faction) -> bool:	
	for connected_country_state:CountryState in connected_countries(Globals.faction_team_for_faction(_faction)):
		if connected_country_state.occupying_factions.has(_faction):
			return true	
	return false
	
func can_attack_when_empty(_faction:Enum.Faction) -> bool:	
	return in_range_for_attack(_faction) && is_country_empty

func is_harbor_for(country_state_1:CountryState, country_state_2:CountryState):
	return (self.harbor1 == country_state_1.name && self.harbor2 == country_state_2.name) || (self.harbor2 == country_state_1.name && self.harbor1 == country_state_2.name)
