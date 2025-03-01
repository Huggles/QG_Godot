class_name CountryData extends DataObject

var name:String
var name_camel_case:String
var clabel:String
var number:int
var type
var is_supply:String
var is_harbor:String
var harbor1
var harbor2
var neighbors = []
var world_pos_x: float
var world_pos_y: float
var world_pos_z: float
var texture

var supply_star_transform_data:TransformData
var unit_transform_data:UnitTransformData


#Properties
var WorldPositionCenter:
	get: return Vector3(world_pos_x, world_pos_z, world_pos_y)/2
var WorldPositionTopLeft:
	get: return Vector3(WorldPositionCenter.x - ImageSize.x/2, WorldPositionCenter.y, WorldPositionCenter.z - ImageSize.y/2)
var ImageSize:
	get: return Vector2(texture.get_width(), texture.get_height())

func _init(json_object:Dictionary):
	super(json_object)	
	_setNeighborCountries(json_object)
		
	var texture_path = "res://assets/textures/Countries/"+name_camel_case+".png"		
	texture = load(texture_path)
	if texture == null: DebugUtilities.print_peer_err(str("Error loading texture: ", texture_path))

func _setNeighborCountries(json_object:Dictionary):
	var array = [];
	for n in 10:
		var key = "neighbor" + str(n+1)		
		if key in json_object: 
			var value = json_object[key]
			if value != null:
				array.push_back(json_object[key])			
	neighbors = array;
	
