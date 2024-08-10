extends DataObject
class_name FactionData

var name:String
var index:int

var clabel:String
var color_string:String
var team:String
var homespace:String


var number_army_units:int
var number_navy_units:int
	
func color()->Color:
	return Color.from_string(color_string, Color.WHITE)
