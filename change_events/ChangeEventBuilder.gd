class_name ChangeEventBuilder extends Object

var game_change_event:GameChangeEvent


func _init() -> void:
    pass

func withTriggeringCardId(_card_id:int):
    game_change_event.triggering_card_Id = _card_id
    pass

func withTriggeringFaction(_faction:Enum.Faction):
    game_change_event.triggering_faction = _faction
    pass

func withTargetCountryId(_country_id:int):
    
    pass

func withTargetUnitId(_unit_id:int):
    pass

func withVictoryPoints(_victory_point:int):
    pass
