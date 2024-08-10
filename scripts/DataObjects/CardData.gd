class_name CardData
extends DataObject


var name:String
var clabel:String
var text:String
var type:String
var execution_class:String


var implemented: int = false
var testing: int = false

func get_card(_faction_data:FactionData)->CardBase:	
	if DataUtilities.class_map.has(execution_class):
		var class_path = DataUtilities.class_map.get(execution_class).path
		var instance = load(class_path).new(self, _faction_data)
		return instance
	return null
