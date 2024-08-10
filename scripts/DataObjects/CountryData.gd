class_name CountryData
extends Object

var name:String
var name_camel_case:String
var clabel:String
var number:int
var type
var is_supply:String
var is_harbor:String
var harbor1
var harbor2
var neightbors = []
var world_pos_x: float
var world_pos_y: float
var world_pos_z: float
var texture

#Properties
var WorldPositionCenter:
	get: return Vector3(world_pos_x, world_pos_z, world_pos_y)/2
var WorldPositionTopLeft:
	get: return Vector3(WorldPositionCenter.x - ImageSize.x/2, WorldPositionCenter.y, WorldPositionCenter.z - ImageSize.y/2)
var ImageSize:
	get: return Vector2(texture.get_width(), texture.get_height())

func _init(json_object:Dictionary):
	for key in json_object.keys():		
		var value = json_object[key]
		if key in self:
			self[key] = value
	_setNeighborCountries(json_object)
		
	var texture_path = "res://assets/textures/World/"+name_camel_case+".png"		
	texture = load(texture_path)
	

func _setNeighborCountries(json_object:Dictionary):
	var array = [];
	for n in 10:
		var key = "Neighbor" + str(n+1)		
		if key in json_object: 
			var value = json_object[key]
			if value != null:
				array.push_back(json_object[key])
			
	neightbors = array;
	
