class_name CardData
extends DataObject


var name:String
var clabel:String
var text:String
var type:String
var execution_class:String
var faction:Enum.Faction


var implemented: int = false
var testing: int = false

func get_card_logic_class()->CardLogicBase:	
	if DataUtilities.class_map.has(execution_class):
		var class_path = DataUtilities.class_map.get(execution_class).path
		var instance:CardLogicBase = load(class_path).new(self)
		return instance
	else:		
		DebugUtilities.print_peer_err(str("Could not find card logic class for: ", clabel))
		return null
