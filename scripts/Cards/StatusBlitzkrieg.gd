class_name StatusBlitzkrieg extends CardLogicStatus

func _can_activate_card(_game_change_event:GameChangeEvent)->bool:
	if _game_change_event is BattleUnitChangeEvent && _game_change_event.triggering_faction == self.faction:
		var _battle_unit_change_event:BattleUnitChangeEvent = _game_change_event	
		if CountryState.for_id(_battle_unit_change_event.country_id).is_country_empty:
			return true		
	return false
	
func _activate_card(_game_change_event:GameChangeEvent):
	var _battle_unit_change_event:BattleUnitChangeEvent = _game_change_event	
	var deploy_unit_change_event:DeployUnitChangeEvent = DeployUnitChangeEvent.new(faction, _battle_unit_change_event.country_id, Enum.DeployType.BUILD)			
	deploy_unit_change_event.source_card_id = card_state.id
	ChangeEventHandler.execute_change_event(deploy_unit_change_event)	
	card_activation_finished();
	
		
	
	
