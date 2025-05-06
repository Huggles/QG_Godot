class_name StatusSyntheticFuel extends CardLogicStatus

func _targetable_countries(_game_change_event:GameChangeEvent) -> Array[int] :
	var _neighbor_countries:Array[int] = CountryState.for_id(_game_change_event.country_id).connected_country_ids(faction).filter(func(_country_id:int): return CountryState.for_id(_country_id).is_land)
	var _buildable_countries:Array[int] = GameManager.game_state.buildable_countries_for_faction(faction).filter(func(_country_id:int): return CountryState.for_id(_country_id).is_land)
	return _buildable_countries.filter(func(_country_id:int): return _neighbor_countries.has(_country_id))

func _can_activate_action(_game_change_event:GameChangeEvent) -> bool:
	if _game_change_event is DeployUnitChangeEvent && _game_change_event.triggering_faction == self.faction && !is_activated_this_turn && UnitPool.faction_has_available_army(faction):
		var _deploy_unit_change_event:DeployUnitChangeEvent = _game_change_event	
		if CountryState.for_id(_deploy_unit_change_event.country_id).is_land && _targetable_countries(_game_change_event).size() > 0:
			return true
	return false

func _activate_action(_game_change_event:GameChangeEvent, _part_counter:int):
	SelectSingleCountryHandler.new(_targetable_countries(_game_change_event)).handle(
		func (_selected_country_id:int):
			var _deploy_unit_change_event:DeployUnitChangeEvent = DeployUnitChangeEvent.new(faction, _selected_country_id, Enum.DeployType.BUILD)			
			_deploy_unit_change_event.source_card_id = card_state.id
			##CardPlayHandler.instance.execute_change_event(_deploy_unit_change_event)
			
			card_activation_finished();
			)

	
	
	
		
	
	
