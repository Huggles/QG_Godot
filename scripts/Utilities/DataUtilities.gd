extends Object
class_name DataUtilities


static var _class_map:Dictionary
static var class_map:Dictionary:
	get: 
		if not _class_map || _class_map.size() == 0:
			var class_list:Array[Dictionary] = ProjectSettings.get_global_class_list()
			for list_item in class_list:
				var class_item = ClassItem.new(list_item.get("class"),list_item.get("base"),list_item.get("path"))
				_class_map[class_item.name] = class_item
		return _class_map


class ClassItem:
	var name:String
	var base:String
	var path:String
	
	func _init(_name:String, _base:String, _path:String)->void:
		self.name = _name
		self.base = _base
		self.path = _path
