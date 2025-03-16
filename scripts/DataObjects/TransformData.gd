class_name TransformData extends DataObject


var scale:float = 1;
var x_position:float = 0;
var y_position:float = 0;
var z_position:float = 0;

var x_rotation:float = 0;
var y_rotation:float = 0;
var z_rotation:float = 0;

var position_vector3:Vector3:
	get: return Vector3(x_position, y_position, z_position)
