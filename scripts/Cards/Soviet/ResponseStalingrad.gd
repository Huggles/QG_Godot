class_name ResponseStalingrad extends CardLogicResponse


func _can_activate_before(_game_change_event:GameChangeEvent):
    if _game_change_event is BattleUnitChangeEvent:
        var _gce:BattleUnitChangeEvent = (_game_change_event as BattleUnitChangeEvent)
        var _target_country:CountryState = CountryState.for_id(_gce.country_id)
        var _target_unit:UnitState = UnitState.for_id(_gce.unit_id)
        return _target_country.name == "UKRAINE" && _target_unit.faction_enum == Enum.Faction.SOVIET
    return false

func _activate_before(_game_change_event:GameChangeEvent):
    var _gce:BattleUnitChangeEvent = (_game_change_event as BattleUnitChangeEvent)
    var _target_country:CountryState = CountryState.for_id(_gce.country_id)
    if _target_country.name == "UKRAINE":
        var _block_gce = BlockChangeEvent.new(self.faction, _game_change_event)
        GameManager.game_state.card_play_handler.execute_change_event(_block_gce)
    