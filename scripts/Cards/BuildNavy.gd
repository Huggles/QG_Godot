class_name BuildNavy extends CardLogicBase

var _buildable_countries:
	get: return  GameManager.game_state.buildable_countries_for_faction(faction).filter(func(_country_id:int): return CountryState.for_id(_country_id).is_sea)		

func can_play_card() -> bool:	
	var _has_available_units = UnitPool.faction_has_available_navy(faction)
	return _buildable_countries.size() > 0 && _has_available_units

func _play_card()->void:	 
	_request_country()	
	
func _request_country():		
	SelectSingleCountryHandler.new(_buildable_countries).handle(
		func (_selected_country_id:int):
			var _deploy_unit_change_event:DeployUnitChangeEvent = DeployUnitChangeEvent.new(faction, _selected_country_id, Enum.DeployType.BUILD)						
			_deploy_unit_change_event.source_card_id = card_state.id
			await ChangeEventHandler.execute_change_event(_deploy_unit_change_event).change_event_finished
			card_play_finished()
			)
			