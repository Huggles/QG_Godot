extends Resource
class_name DataObject

func stringify() -> String:
	var object_as_dictionary:Dictionary = {}				
	var flags = PROPERTY_USAGE_SCRIPT_VARIABLE
	
	for prop in self.get_property_list():
		if(prop.usage & flags > 0):
			object_as_dictionary[prop.name] = self.get(prop.name)
	
	return JSON.stringify(object_as_dictionary)

func parse(json_object:Dictionary)->DataObject:
	for key in json_object.keys():		
		var value = json_object[key]
		if key in self:
			self[key] = value
	return self

func parse_string(json_string:String)->DataObject:
	return parse(JSON.parse_string(json_string))
	
func _init(json):		
	if json is String:
		json = JSON.parse_string(json)		
	for key in json.keys():		
		var value = json[key]
		if key in self:			
			if not value == null:
				self[key] = value
	
	
