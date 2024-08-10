extends Object
class_name CardBase


var card:CardData
var faction:FactionData

var is_publicly_visible:bool


func _init(_card_data:CardData, _faction_data:FactionData)->void:
	card = _card_data
	faction = _faction_data

func execute_card()->bool:
	return false
