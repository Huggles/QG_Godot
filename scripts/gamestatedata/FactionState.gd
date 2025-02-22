extends DataObject
class_name FactionState

var faction:Enum.Faction;
var score = 0;

func _init(_faction:Enum.Faction) -> void:
	self.faction = _faction;
	return
