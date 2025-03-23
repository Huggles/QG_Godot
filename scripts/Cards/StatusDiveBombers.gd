class_name StatusDiveBombers extends CardLogicStatus

func _targetable_countries(_initial_country_id:int) -> Array[CountryState]:
	var _cs = CountryState.for_id(_initial_country_id)
	return _cs.connected_countries(self.faction).filter(
		func(_cc:CountryState): 
			return _cc.in_range_for_attack(faction) && _cc.is_land && _cc.occupying_team == StaticGameData.opponent_faction_team_for_faction(self.faction)
			)
		

func _can_activate_card(_game_change_event:GameChangeEvent)->bool:
	if _game_change_event is BattleUnitChangeEvent && _game_change_event.triggering_faction == self.faction:
		var _battle_unit_change_event:BattleUnitChangeEvent = _game_change_event	
		if _targetable_countries(_battle_unit_change_event.country_id).size() > 0:
			return true		
	return false
	
func _activate_card(_game_change_event:GameChangeEvent):
	var _original_battle_unit_change_event:BattleUnitChangeEvent = _game_change_event	
	var _unit_ids:Array[int] = []
	for _target_country in _targetable_countries(_original_battle_unit_change_event.country_id):
		_unit_ids.append_array(_target_country.units.values())
	SelectSingleUnitHandler.new(_unit_ids).handle(
		func(_selected_unit_id):
			var _battle_unit_change_event:BattleUnitChangeEvent = BattleUnitChangeEvent.new(faction, _selected_unit_id)		
			_battle_unit_change_event.source_card_id = card_state.id				
			ChangeEventHandler.execute_change_event(_battle_unit_change_event)			
			card_activation_finished()
	)	
	
		
	
	
