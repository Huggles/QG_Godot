class_name DataObject

var data_class:String

func stringify() -> String:
	var object_as_dictionary:Dictionary = {}				
	var flags = PROPERTY_USAGE_SCRIPT_VARIABLE
	
	for prop in self.get_property_list():
		var value = self.get(prop.name)
		if value is DataObject:
			object_as_dictionary[prop.name] = value.stringify()
		if(prop.usage & flags > 0):
			object_as_dictionary[prop.name] = value
	
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
	for _key in json.keys():		
		var _value = json[_key]
		if _key in self and not _value == null:			
			if _value is Dictionary:
				var _data_class = _value.get("data_class")
				if _data_class != null: 
					self[_key] = _init_child_object(_key,_value, _data_class)				
			else:
				self[_key] = _value
				
func _init_child_object(_key:String, _value:Dictionary, _data_class:String) -> DataObject:		
	var _class_ref = DataUtilities.class_map.get(_data_class)
	if _class_ref == null: 
		DebugUtilities.print_peer_err(str("Couldnt find clas: ", _data_class))
		return null		
	var _class_path = _class_ref.path
	var instance:DataObject = load(_class_path).new(_value)
	return instance

func class_for_key(_key:String) -> String:
	return "";
	
