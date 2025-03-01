class_name BuildNavy extends CardLogicBase

func can_execute_card() -> bool:
	return UnitPool.faction_has_available_navy(faction)

func _start_card()->void:	 
	print("BuildArmy.execute_card")
	_request_country()	
	
func _request_country():		
	GameManager.game_state.request_single_country_selection(
		CountryState.for_ids(GameManager.game_state.buildable_countries_for_faction(faction)).filter(func(_country_state:CountryState): return _country_state.type == Enum.CountryType.SEA),
		faction, 
		func(_selected_country_id:int):		
			if _selected_country_id >= 0:
				var deploy_unit_change_event:DeployUnitChangeEvent = DeployUnitChangeEvent.new(faction, _selected_country_id, Enum.DeployType.BUILD)			
				deploy_unit_change_event.apply_change()						
				#card_execution_finished();
	)
	
