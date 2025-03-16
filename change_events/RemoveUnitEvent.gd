class_name RemoveUnitChangeEvent extends GameChangeEvent

var unit_id:int
var country_id:int
var reason:Enum.UnitRemovalReason

func _init(_triggering_faction:Enum.Faction, _unit_id:int, _removal_reason:Enum.UnitRemovalReason) -> void:		
	super(_triggering_faction)
	self.unit_id 	= _unit_id
	self.country_id = UnitState.for_id(self.unit_id).country_id
	self.reason 	= _removal_reason

func _apply_change_event():
	GameStateUtilities.game_state.remove_unit_from_country(self.unit_id)

func display_text() -> String:	
	return str(Enum.Faction.keys()[triggering_faction], " removed unit from ", CountryState.for_id(country_id).clabel)	 
