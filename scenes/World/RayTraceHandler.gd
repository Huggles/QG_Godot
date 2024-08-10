extends Area3D

var _mouse_input_received := false
var mouse_previously_over_opaque := false
var mouse_currently_over_opaque := false
var image : Image

signal mouse_entered_opaque
signal mouse_exited_opaque

func _ready():
	var camera = get_viewport().get_camera_3d()
	
	if camera && camera.has_signal("mouse_ray_processed"):
		camera.mouse_ray_processed.connect(_on_3d_mouse_ray_processed)

func _on_3d_mouse_ray_processed() -> void:
	if _mouse_input_received:
		if !mouse_previously_over_opaque && mouse_currently_over_opaque:	
			mouse_entered_opaque.emit()	
	elif mouse_currently_over_opaque:
		mouse_currently_over_opaque = false
		mouse_exited_opaque.emit()		
	
	mouse_previously_over_opaque = mouse_currently_over_opaque
	_mouse_input_received = false
	
func try_mouse_input(_camera: Node, _event: InputEvent, _input_position:Vector3, _normal: Vector3) -> bool:
	_mouse_input_received = true;
	mouse_currently_over_opaque = self.get_parent().is_pixel_opaque(_input_position)	
	return true
