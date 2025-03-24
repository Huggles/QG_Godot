class_name EventBroadFront extends CardLogicBase


var _attackable_unit_ids:Array[int]    
const _MAX_ACTIONS = 3

func _has_next_action() -> bool:
    return (part_counter) < _MAX_ACTIONS

func _can_play_card(_part:int) -> bool:		
    if _part == 1:
        _attackable_unit_ids = GameManager.game_state.attackable_units_for_faction(faction).filter(func(_unit_id:int): return UnitState.for_id(_unit_id).country_state.is_land )
        _attackable_unit_ids = _attackable_unit_ids.filter(func(_unit_id:int): return UnitState.for_id(_unit_id).country_state.units.keys().has(Enum.Faction.SOVIET))
    return _attackable_unit_ids.size() > 0

func _play_card(_part:int)->void:
    if _part == 1:
        _attackable_unit_ids = GameManager.game_state.attackable_units_for_faction(faction).filter(func(_unit_id:int): return UnitState.for_id(_unit_id).country_state.is_land )
        _attackable_unit_ids = _attackable_unit_ids.filter(func(_unit_id:int): return UnitState.for_id(_unit_id).country_state.units.keys().has(Enum.Faction.SOVIET))        
    
    SelectSingleUnitHandler.new(_attackable_unit_ids).handle(
        func(_selected_unit_id):
            var _battle_unit_change_event:BattleUnitChangeEvent = BattleUnitChangeEvent.new(faction, _selected_unit_id)		
            _battle_unit_change_event.source_card_id = card_state.id				
            ChangeEventHandler.execute_change_event(_battle_unit_change_event)
            print("execute_change_event")
            card_play_finished()
            )

func _play_action_guidance(_part:int) -> String:
    return str("Select a unit for ", card_data.clabel, "(", _part, "/", _MAX_ACTIONS, ")")
			

