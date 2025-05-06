class_name CardActivationOption

var card_id:int
var type:String
var change_event_id:int
var is_before:bool


var card_state:CardState:
    get: return CardState.for_id(card_id)
var change_event:GameChangeEvent:
    get: return GameChangeEvent.for_id(change_event_id)


func _init(_card_id:int, _type:String, _is_before:bool=false) -> void:
    self.card_id = _card_id
    self.type = _type
    self.is_before = _is_before

