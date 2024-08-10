class_name PlayerCamera3D
extends Camera3D

const RAY_LENGTH = 10000 
var dragging = false

signal mouse_ray_processed()
@onready var _player = $".."



const ZOOM_SPEED: float = 100
const MIN_ZOOM_Y: float = 1000
const MAX_ZOOM_Y: float = 100

var ZOOM_RATIO:
	get:
		var ratio = _player.position.y / (MIN_ZOOM_Y - MAX_ZOOM_Y)
		return ratio


@export_flags_3d_physics var _sprite_layers

var _query_mouse := false
var _mouse_event : InputEventMouse

func _unhandled_input(event):
	if event is InputEventMouse:
		_query_mouse = true
		_mouse_event = event
	if event is InputEventMouseMotion:
		if Input.is_action_pressed("camera_drag"):
			_player.position -= Vector3(event.relative.x, 0, event.relative.y) * ZOOM_RATIO
		if Input.is_action_pressed("camera_drag"):
			pass
	if event is InputEventMouseButton:
			if Input.is_action_pressed("game_zoom_in"):
				_zoom_in()
			if Input.is_action_pressed("game_zoom_out"):
				_zoom_out()

func _physics_process(_delta):
	if _query_mouse:
		_check_sprite_input()
		_query_mouse = false
		mouse_ray_processed.emit()
	
		
func _check_sprite_input() -> bool:
	var not_hits = []
	
	var space_state = get_world_3d().direct_space_state
	var from = project_ray_origin(_mouse_event.position)
	var to = from + project_ray_normal(_mouse_event.position) * RAY_LENGTH
	
	while true:
		var query = PhysicsRayQueryParameters3D.create(from, to, _sprite_layers, not_hits)
		query.collide_with_areas = true;
		var result = space_state.intersect_ray(query)
		if result.is_empty():
			return false		
		
		var collision_object = result.collider		
		if collision_object.has_method("try_mouse_input"):				
			return collision_object.try_mouse_input(self, _mouse_event, result.position, result.normal)		
			
		else:
			not_hits.append(result.collider)
			
	return true
	
func _zoom_in() -> void:
	print('zoom in')	
	var _zoom_vector = -(self.transform.basis.z) * ZOOM_SPEED
	var _new_position = _player.position + _zoom_vector
	if _new_position.y > MAX_ZOOM_Y:
		_player.position = _new_position
	
func _zoom_out() -> void:
	print('zoom out')
	var _zoom_vector = (self.transform.basis.z) * ZOOM_SPEED
	var _new_position = _player.position + _zoom_vector
	if _new_position.y < MIN_ZOOM_Y:
		_player.position = _new_position
	
