class_name BuildArmy extends CardLogicBase

func can_play_card() -> bool:	
	var _buildable_countries = GameManager.game_state.buildable_land_countries_for_faction(faction)
	var _has_available_units = UnitPool.faction_has_available_army(faction)
	return _buildable_countries.size() > 0 && _has_available_units

func _play_card()->void:	 	
	_request_country()	
	
func _request_country():	
	var _buildable_countries = GameManager.game_state.buildable_countries_for_faction(faction).filter(func(_country_id:int): return CountryState.for_id(_country_id).is_land)		
	SelectSingleCountryHandler.new(_buildable_countries).handle(
		func (_selected_country_id:int):
			print("click callback in BuildArmy")
			var _deploy_unit_change_event:DeployUnitChangeEvent = DeployUnitChangeEvent.new(faction, _selected_country_id, Enum.DeployType.BUILD)			
			_deploy_unit_change_event.source_card_id = card_state.id
			ChangeEventHandler.execute_change_event(_deploy_unit_change_event)
			card_play_finished()
			)
