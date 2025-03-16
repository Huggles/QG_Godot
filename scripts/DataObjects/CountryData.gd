class_name CountryData extends DataObject

var name:String
var name_camel_case:String
var clabel:String
var number:int
var type
var is_supply:String
var neighbors = []

var texture

var straight_data:StraightData
var world_transform:TransformData
var supply_star_transform_data:TransformData
var unit_transform_data:UnitTransformData


#Properties
var WorldPositionCenterUnscaled:Vector3:
	get: return Vector3(world_transform.x_position,-world_transform.y_position, world_transform.z_position)
var WorldPositionCenter:Vector3:
	get:
		const scale = 4
		return Vector3((WorldPositionCenterUnscaled.x / 100) * scale, (WorldPositionCenterUnscaled.y / 100) * scale, (WorldPositionCenterUnscaled.z + 1)) - Vector3(149, -50, 0)
		

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
	
