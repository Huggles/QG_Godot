class_name LandBattle extends CardLogicBase

var _attackable_units:Array[int]:
	get: return GameManager.game_state.attackable_units_for_faction(faction).filter(func(_unit_id:int): return UnitState.for_id(_unit_id).country_state.is_land)	

func can_play_card() -> bool:		
	return _attackable_units.size() > 0

func _play_card()->void:	 	
	_request_country()	

func _request_country():	
	SelectSingleUnitHandler.new(_attackable_units).handle(
		func(_selected_unit_id):
			var _battle_unit_change_event:BattleUnitChangeEvent = BattleUnitChangeEvent.new(faction, _selected_unit_id)		
			_battle_unit_change_event.source_card_id = card_state.id				
			ChangeEventHandler.execute_change_event(_battle_unit_change_event)
			print("execute_change_event")
			card_play_finished()
	)	
			


	
