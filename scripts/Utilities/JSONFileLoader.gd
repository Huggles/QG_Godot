extends Object
class_name JSONFileLoader

var _json_string
var _json_object

func _init(filename:String):
	if FileAccess.file_exists(filename):
		var dataFile = FileAccess.open(filename, FileAccess.READ)
		_json_string = dataFile.get_as_text()
		_json_object = JSON.parse_string(_json_string)	
		
		
func get_json_string() -> String:
	return _json_string	
	
func get_json_array() -> Array:	
	return _json_object
		
func get_json_object() -> Dictionary:
	return _json_object
		
	
