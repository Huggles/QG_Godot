class_name GameState extends DataObject

var game_mode:GameMode
var faction_states:Dictionary
var game_change_events:Array[GameChangeEvent]
var card_play_handler:CardPlayHandler

var active_player_card:CardState

signal COUNTRY_CLICKED

#List of country states, containing live data about the countries in this game. 
var country_states:Array[CountryState]
var country_state_by_id:Dictionary:
	get: 
		if country_state_by_id || country_state_by_id.size() == 0:
			for country_state in country_states:	
				country_state_by_id[country_state.id] = country_state
		return country_state_by_id
		
var country_state_by_name:Dictionary:
	get: 
		if country_state_by_name || country_state_by_name.size() == 0:
			for country_state in country_states:	
				country_state_by_name[country_state.name] = country_state
		return country_state_by_name
		
var straight_states:Array[StraightState]
var straight_state_by_controlling_country_id:Dictionary:
	get: 
		if straight_state_by_controlling_country_id || straight_state_by_controlling_country_id.size() == 0:
			for _straight_state in straight_states:	
				straight_state_by_controlling_country_id[_straight_state.controlling_country_id] = _straight_state
		return straight_state_by_controlling_country_id
	
		
var unit_states:Array[UnitState]
var unit_states_by_id:Dictionary:
	get: 
		if unit_states_by_id || unit_states_by_id.size() == 0:
			for unit_state in unit_states:	
				unit_states_by_id[unit_state.id] = unit_state
		return unit_states_by_id

var card_states:Array[CardState]
var card_states_by_id:Dictionary:
	get: 
		if card_states_by_id || card_states_by_id.size() == 0:
			for card_state in card_states:	
				card_states_by_id[card_state.id] = card_state
		return card_states_by_id
		
var card_states_by_name:Dictionary:
	get: 
		if card_states_by_name || card_states_by_name.size() == 0:
			for _card_state in card_states:	
				card_states_by_name[_card_state.card_data.name] = _card_state
		return card_states_by_name

func _init() -> void:	
	EventBusLocal.recalculate_supply.connect(recalculate_supply)		
	EventBusLocal.recalculate_straights.connect(recalculate_straights)
	return

func recalculate_supply():
	for _faction in Enum.Faction.values():		
		_recalculate_supply_for_faction(_faction)

func _recalculate_supply_for_faction(_faction:Enum.Faction):
	var _faction_state:FactionState = faction_states[_faction]
	var _path_finding_service:PathFindingService = PathFindingService.new(IPathFindingNode.new(), _faction)	
	var _active_units_for_faction = UnitState.for_ids(_faction_state.active_unit_ids)
	var _active_army_units_for_faction = _active_units_for_faction.filter(func(_us:UnitState): return _us.type == Enum.UnitType.ARMY )
	var _active_navy_units_for_faction = _active_units_for_faction.filter(func(_us:UnitState): return _us.type == Enum.UnitType.NAVY )
	for _unit_state:UnitState in _active_army_units_for_faction:
		var _unit_supplied:bool = _recalculate_supply_for_unit_id(_path_finding_service, _unit_state.id)						
		if _unit_supplied: 
			_unit_state.set_in_supply()
			continue			
		else:
			_unit_state.set_out_of_supply()
	
	for _unit_state:UnitState in _active_navy_units_for_faction:
		var _unit_supplied:bool = _recalculate_supply_for_unit_id(_path_finding_service, _unit_state.id)						
		if _unit_supplied: 
			_unit_state.set_in_supply()
			continue			
		else:
			_unit_state.set_out_of_supply()

func _recalculate_supply_for_unit_id(_path_finding_service:PathFindingService, _unit_id:int):
	var _unit_state:UnitState = UnitState.for_id(_unit_id)
	for _supply_country_id in GameStateUtilities.get_supply_country_ids(_unit_state.faction_enum):		
		var has_path:bool = _path_finding_service.calculate_path(_unit_state.country_id,_supply_country_id)				
		if has_path: 
			if _unit_state.is_navy:
				return _unit_state.country_state.has_harbor(_unit_state.faction_enum)
			else:
				return true
	DebugUtilities.print_peer(str("Unit is now out of supply in ", _unit_state.country_state.clabel ," (",_unit_state.faction, ")"))
	return false		

func recalculate_straights():
	for _straight_state:StraightState in straight_states:
		_straight_state.recalulate_controlled_by()
	pass
	
func faction_state_for_enum(_faction:Enum.Faction) -> FactionState:
	return faction_states[_faction]

func deploy_unit_to_country(_country_id:int, _faction:Enum.Faction, _unit_type:Enum.UnitType, _deploy_type:Enum.DeployType) -> void:		
	if(_country_id == null || _faction == null || _unit_type == null):		
		return
	var _unit_id:int = UnitPool.get_available_unit_for_faction(_faction,_unit_type)	
	var _unit_state:UnitState = UnitState.for_id(_unit_id)
	var _country_state:CountryState = country_state_by_id[_country_id];
	var deployable = _country_state.can_build(_faction) if _deploy_type == Enum.DeployType.BUILD else true
	if _country_state.is_country_full == false && deployable:	
		_unit_state.BEFORE_UNIT_DEPLOYED_TO_COUNTRY.emit();
		_country_state.units[_unit_state.faction_enum] = _unit_state.id
		_unit_state.country_id = _country_id
		_unit_state.AFTER_UNIT_DEPLOYED_TO_COUNTRY.emit();
	return
	
func remove_unit_from_country(_unit_id:int) -> void:
	if(_unit_id == null):
		return	
	var _unit_state:UnitState = UnitState.for_id(_unit_id)	
	var _country_state:CountryState = CountryState.for_id(_unit_state.country_id)
	_unit_state.BEFORE_UNIT_REMOVED_FROM_COUNTRY.emit(_unit_id, _country_state.id);
	_country_state.units.erase(_unit_state.faction_enum)
	_unit_state.country_id = -1
	_unit_state.AFTER_UNIT_REMOVED_FROM_COUNTRY.emit();
	return
		
func attack_unit(_unit_id:int) -> void:	
	if(_unit_id == null):		
		return		
	var _unit_state:UnitState = unit_states_by_id[_unit_id];	
	var _country_id = _unit_state.country_id

	_unit_state.BEFORE_UNIT_REMOVED_FROM_COUNTRY.emit(_unit_id, _country_id);
	_unit_state.country_state.units.erase(_unit_state.faction_enum)
	_unit_state.country_id = -1;
	_unit_state.AFTER_UNIT_REMOVED_FROM_COUNTRY.emit();
	return	
	
func eliminate_unit(_unit_id:String) -> void:	
	return
	
func score_victory_points(_faction:Enum.Faction, _victory_points:int) -> void:	
	var fs:FactionState = self.faction_states[_faction]
	fs.score += _victory_points
	return

func hand_discard(_card_ids:Array[String]) -> void:	
	return

func force_discard(_card_id:String) -> void:	
	return
		
func force_draw_deck_discard(_faction:String, _number_of_cards:int) -> void:	
	return

func play_card(_card_id:String) -> void:	
	return
	
func activate_status_card(_card_id:String) -> void:	
	return

func activate_response_card(_card_id:String) -> void:	
	return
	
func buildable_countries_for_faction(_faction:Enum.Faction) -> Array[int]:
	var response:Array[int]	= []
	var supplied_unit_ids = GameStateUtilities.supplied_units_for_faction(_faction)	
	if supplied_unit_ids.size() > 0:
		for _country_state in CountryState.for_unit_ids(supplied_unit_ids):
			for _neighbor_country_state in _country_state.neighbor_country_states:
				if _neighbor_country_state.can_build(_faction):
					response.push_back(_neighbor_country_state.id)
	return response
	
func buildable_land_countries_for_faction(_faction:Enum.Faction) -> Array[int]:
	return buildable_countries_for_faction(_faction).filter(
		func(_country_id:int): return CountryState.for_id(_country_id).type == Enum.CountryType.LAND )
func buildable_sea_countries_for_faction(_faction:Enum.Faction) -> Array[int]:
	return buildable_countries_for_faction(_faction).filter(
		func(_country_id:int): return CountryState.for_id(_country_id).type == Enum.CountryType.SEA )
	
func attackable_countries_for_faction(_faction:Enum.Faction) -> Array[int]:
	var response:Array[int]	= []
	var supplied_unit_ids = GameStateUtilities.supplied_units_for_faction(_faction)	
	if supplied_unit_ids.size() > 0:
		for _country_state in CountryState.for_unit_ids(supplied_unit_ids):
			for _connected_country_state in _country_state.connected_countries(_faction):
				if _connected_country_state.can_attack_when_empty(_faction):
					response.push_back(_connected_country_state.id)
	return response

func attackable_units_for_faction(_faction:Enum.Faction) -> Array[int]:
	var response:Array[int]	= []
	var supplied_unit_ids = GameStateUtilities.supplied_units_for_faction(_faction)	
	if supplied_unit_ids.size() > 0:
		for _country_state in CountryState.for_unit_ids(supplied_unit_ids):
			for _connected_country_state in _country_state.connected_countries(_faction):
				if _connected_country_state.occupying_team == StaticGameData.opponent_faction_team_for_faction(_faction):
					response.append_array(_connected_country_state.units.values())					
	return response

	
