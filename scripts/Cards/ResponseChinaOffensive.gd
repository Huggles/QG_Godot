class_name ResponseChinaOffensive extends CardLogicResponse
 
func _can_activate_action(_game_change_event:GameChangeEvent)->bool:
    if (_game_change_event is BattleUnitChangeEvent && _game_change_event.triggering_faction == self.faction):
        var _battle_unit_change_event:BattleUnitChangeEvent = _game_change_event	
        if (CountryState.for_id(_battle_unit_change_event.country_id).name == "CHINA"): 
            return true
        else:
            return false
    return false


	
func _activate_action(_game_change_event:GameChangeEvent):
    var _battle_unit_change_event:BattleUnitChangeEvent = _game_change_event 
    var _country_state_china = CountryState.for_name("CHINA")
    var deploy_unit_change_event:DeployUnitChangeEvent = DeployUnitChangeEvent.new(faction, _country_state_china.id, Enum.DeployType.BUILD)
    deploy_unit_change_event.source_card_id = card_state.id
    CardPlayHandler.instance.execute_change_event(deploy_unit_change_event)
    card_activation_finished();
	
 
		
	
	
