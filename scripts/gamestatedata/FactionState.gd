class_name FactionState extends StateObject

var faction_data:FactionData
var faction:Enum.Faction:
	get: return faction_data.faction_enum
var faction_label:String:
	get: return Enum.Faction.keys()[faction]
var score:int:
	get: 
		return score
	set(value):
		score = value
		EventBusLocal.faction_scored_points.emit(faction, score)


var deck_state:DeckState

var all_units:Array[int]:
	get:		
		var response:Array[int]
		for _unit_state in GameManager.game_state.unit_states:
			if _unit_state.faction_enum == faction:
				response.push_back(_unit_state.id)
		return response
		
var active_unit_ids:Array[int]:
	get:
		var response:Array[int]
		for _unit_state in UnitState.for_ids(all_units):
			if _unit_state.country_id >= 0: 
				response.push_back(_unit_state.id)
		return response
		
var occupied_country_ids:Array[int]:
	get:
		var response:Array[int]
		for _unit_id in active_unit_ids:			
			response.push_back(GameManager.game_state.unit_states_by_id[_unit_id].country_id)							
		return response

var supplied_unit_ids:Array[int]:
	get:
		var response:Array[int]
		for _unit_id in active_unit_ids:		
			var _unit_state:UnitState = GameManager.game_state.unit_states_by_id[_unit_id]
			if _unit_state.in_supply:
				response.push_back(_unit_id)							
		return response
		
var unsupplied_unit_ids:Array[int]:
	get:
		var response:Array[int]
		for _unit_id in active_unit_ids:		
			var _unit_state:UnitState = GameManager.game_state.unit_states_by_id[_unit_id]
			if !_unit_state.in_supply:
				response.push_back(_unit_id)							
		return response

func _init(_faction_data:FactionData) -> void:
	self.faction_data = _faction_data;
	self.score = 0	
	self.deck_state = DeckState.new(self)
	return
