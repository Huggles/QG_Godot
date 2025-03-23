class_name CountryState extends StateObject

var static_country_data:CountryData

var id:int
var name:String
var name_camel_case:String
var clabel:String
var type:Enum.CountryType
var is_supply:bool
var neighbors = []
var neighbor_country_states:Array[CountryState] = []	

var straight_state:StraightState

#Map of FactionEnum -> Unit Id
var units:Dictionary = {}
var is_land:bool:
	get: return self.type == Enum.CountryType.LAND
var is_sea:bool:
	get: return self.type == Enum.CountryType.SEA
var is_country_empty:bool:
	get: return self.units.size() == 0
var is_country_full:bool:
	get: return self.units.size() == 3
var occupying_factions:Array[Enum.Faction]:
	get:
		var of:Array[Enum.Faction]
		of.assign(units.keys())
		return of

var occupying_team:Enum.FactionTeam:
	get: 
		if occupying_factions.size() == 0:
			return Enum.FactionTeam.NONE
		else:
			return StaticGameData.faction_team_for_faction(occupying_factions[0])
			
var node:CountryScene



func _init(_country_data:CountryData) -> void:
	self.static_country_data = _country_data
	self.id = 				_country_data.number
	self.name = 				_country_data.name
	self.name_camel_case = 	_country_data.name_camel_case
	self.clabel = 			_country_data.clabel	
	self.type = 				_country_data.type
	self.is_supply = 		true if _country_data.is_supply == "true" else false	
	self.neighbors = 		_country_data.neighbors
	
	EventBusLocal.set_countries_clickable.connect(set_clickable	)
	EventBusLocal.set_all_countries_unclickable.connect(set_unclickable)
	
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
	
func set_clickable(_country_ids:Array[int]):
	if _country_ids.has(self.id):
		node.set_clickable(func(_country_scene:CountryScene): EventBusLocal.country_clicked.emit(self.id))	

func set_unclickable():
	node.set_unclickable()

func connected_country_ids(_faction:Enum.Faction) -> Array[int]:
	var cc_ids:Array[int] = [];
	for _cc:CountryState in connected_countries(_faction):
		cc_ids.push_back(_cc.id)
	return cc_ids

func connected_countries(_faction:Enum.Faction) -> Array[CountryState]:
	var cc:Array[CountryState] = [];
	cc = neighbor_country_states.filter(
		func(_neighbor_country_state:CountryState):
			if self.is_sea && _neighbor_country_state.is_sea:
				var _straight_state:StraightState = GameStateUtilities.straight_state_for_neighbors(self.id, _neighbor_country_state.id)				
				return _straight_state == null || _straight_state.controlling_country_state.occupying_team == StaticGameData.faction_team_for_faction(_faction)
			else:
				return true
				)
	return cc
	
func has_harbor(_faction:Enum.Faction) -> bool:
	return self.is_sea && connected_countries(_faction).any(
					func(_connected_country_state:CountryState): 
						return _connected_country_state.is_land && _connected_country_state.occupying_team == StaticGameData.faction_team_for_faction(_faction)
						)
	

func deploy_unit():
	pass
	
func remove_unit():
	pass

func can_build(_faction:Enum.Faction) -> bool:
	var _can_build = true
	_can_build = _can_build && can_recruit(_faction)
	_can_build = _can_build && !self.occupying_factions.has(_faction) #This faction is not there already
	_can_build = _can_build && (self.occupying_team != StaticGameData.opponent_faction_team_for_faction(_faction)) #The other faction doesn't control it yet
	if self.type == Enum.CountryType.SEA: #Check if theres a harbor
		var _faction_team = StaticGameData.faction_team_for_faction(_faction)
		_can_build = _can_build && neighbor_country_states.any(
			func(_neighbor_country_state:CountryState): return _neighbor_country_state.type == Enum.CountryType.LAND && _neighbor_country_state.occupying_team == _faction_team
			)	
	return _can_build;
	
func can_recruit(_faction:Enum.Faction) -> bool:
	var _can_recruit = true
	_can_recruit = _can_recruit && (occupying_team == Enum.FactionTeam.NONE || !occupying_factions.has(_faction))	
	return _can_recruit;
	
func in_range_for_attack(_faction:Enum.Faction) -> bool:	
	for _ccs:CountryState in connected_countries(_faction):
		if _ccs.occupying_factions.has(_faction) && UnitState.for_id(_ccs.units.get(_faction)).in_supply:
			return true	
	return false
	
func can_attack_when_empty(_faction:Enum.Faction) -> bool:	
	return in_range_for_attack(_faction) && is_country_empty	
	
static func for_id(_country_id:int)->CountryState:
	return GameManager.game_state.country_state_by_id.get(_country_id)
	
static func for_ids(_country_ids:Array[int]) -> Array[CountryState]:
	var _country_states:Array[CountryState] = []
	for _country_id in _country_ids:
		_country_states.push_back(for_id(_country_id))
	return _country_states

static func for_name(_country_name:String) -> CountryState:
	return GameManager.game_state.country_state_by_name.get(_country_name)
	
static func for_unit_ids(_unit_ids:Array[int]) -> Array[CountryState]:	
	var _country_ids:Array[int] = []
	for _unit_state in UnitState.for_ids(_unit_ids):
		_country_ids.push_back(_unit_state.country_id)	
	return for_ids(_country_ids)
