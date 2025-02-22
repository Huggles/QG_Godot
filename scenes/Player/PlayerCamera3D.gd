class_name PlayerCamera3D
extends Camera3D


var dragging = false

@export var camera_speed:float = 10;
@onready var _player = $".."

const INITIAL_POSITION:Vector3 = Vector3(0,1500,0)
const ZOOM_STEP:float = 100
const MIN_ZOOM_LEVEL:float = -10
const MAX_ZOOM_LEVEL:float = 10
var ZOOM_LEVEL:int = 0;

var ray_trace_caster:RayTraceCaster


var mouse_position:
	get: return get_viewport().get_mouse_position()

func enable_ray_trace_casting():	
	if ray_trace_caster == null:
		ray_trace_caster = RayTraceCaster.new(self)

func disable_ray_trace_casting():
	if ray_trace_caster != null:
		ray_trace_caster = null
	
func _unhandled_input(event):
	if event is InputEventMouseButton:
		if ray_trace_caster != null:
			ray_trace_caster.cast_rays(event)
		if Input.is_action_pressed("game_zoom_in"):
			_zoom_in()
		if Input.is_action_pressed("game_zoom_out"):
			_zoom_out()
	
func _process(delta: float) -> void:
	_keyboard_movement();	
	if ray_trace_caster != null:
		ray_trace_caster.cast_rays(null)		
	
func _zoom_in() -> void:
	print('zoom in')	
	if ZOOM_LEVEL > MIN_ZOOM_LEVEL:
		ZOOM_LEVEL -= 1;
		_zoom();	
	
func _zoom_out() -> void:
	print('zoom out')	
	if ZOOM_LEVEL < MAX_ZOOM_LEVEL:
		ZOOM_LEVEL += 1;	
		_zoom();
		
func _zoom() -> void:		
	self.position.y = INITIAL_POSITION.y + (ZOOM_LEVEL * ZOOM_STEP)	
		
func _keyboard_movement() -> void:
	var input_up:int = Input.is_action_pressed("ui_up");
	var input_down:int = Input.is_action_pressed("ui_down");
	var input_left:int = Input.is_action_pressed("ui_left");
	var input_right:int = Input.is_action_pressed("ui_right");
	
	var zoom_multiplier = 10 - ZOOM_LEVEL; #Move faster when zoomed in.
	var x_delta:float = (-input_left + input_right) * camera_speed;
	var z_delta:float = (-input_up + input_down) * camera_speed;
	
	var delta:Vector3 = Vector3(x_delta, 0, z_delta) * clamp(zoom_multiplier, 3,20)
	self.position += delta
	return
	
