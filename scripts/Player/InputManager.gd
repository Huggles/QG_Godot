class_name InputManager extends Node3D

const INITIAL_POSITION:Vector3 = Vector3(0, 0, 150)
const ZOOM_STEP:float = 5
const CAMERA_SPEED:float = 0.5
const MIN_ZOOM_LEVEL:float = -10
const MAX_ZOOM_LEVEL:float = 10
const HAND_CARD_KEYS = [KEY_0,KEY_1,KEY_2,KEY_3,KEY_4,KEY_5,KEY_6]

var camera:PlayerCamera3D:
	get: return %Camera3D

var mouse_position:
	get: return get_viewport().get_mouse_position()

var ray_trace_caster:RayTraceCaster
var _zoom_level:int = 0;

var currently_pressed_mouse_buttons:Array[InputEventMouseButton]
var previously_pressed_mouse_buttons:Array[InputEventMouseButton]
var currently_pressed_keyboard_buttons:Array[InputEventKey]
var previously_pressed_keyboard_buttons:Array[InputEventKey]

var input_handler:IInputHandler

signal key_clicked

func set_play_card_input_active() -> InputHandlerPlayCard:	
	self.input_handler = InputHandlerPlayCard.new()
	return self.input_handler
	
func set_activate_card_input_active(_faction:Enum.Faction) -> InputHandlerActivateCard:
	self.input_handler = InputHandlerActivateCard.new(_faction)
	return self.input_handler
	
func set_discard_input_active() -> InputHandlerDiscardCard:
	self.input_handler = InputHandlerDiscardCard.new()
	return self.input_handler

func set_debug_input_active() -> InputHandlerDebug:
	self.input_handler = InputHandlerDebug.new()
	return self.input_handler

func set_no_input_active():	
	self.input_handler = null

func _init() -> void:
	EventBusLocal.set_units_clickable.connect(func(_units):enable_ray_trace_casting())
	EventBusLocal.set_countries_clickable.connect(func(_units):enable_ray_trace_casting())	
	EventBusLocal.set_all_units_unclickable.connect(func():disable_ray_trace_casting())
	EventBusLocal.set_all_countries_unclickable.connect(func():disable_ray_trace_casting())
	key_clicked.connect(self.handle_key_clicked)
	
func _process(_delta: float) -> void:
	_keyboard_movement();	
	if ray_trace_caster != null:
		ray_trace_caster.cast_rays(null)	
	
func _keyboard_movement() -> void:
	if camera == null: return
	var input_up:int = Input.is_action_pressed("ui_up");
	var input_down:int = Input.is_action_pressed("ui_down");
	var input_left:int = Input.is_action_pressed("ui_left");
	var input_right:int = Input.is_action_pressed("ui_right");
	
	var zoom_multiplier = 5 - _zoom_level; #Move faster when zoomed in.
	var x_delta:float = (-input_left + input_right) * CAMERA_SPEED;
	var y_delta:float = (input_up + -input_down) * CAMERA_SPEED;
	
	var delta:Vector3 = Vector3(x_delta, y_delta, 0) * clamp(zoom_multiplier, 1,5)
	camera.position += delta
	return

func _input(_event: InputEvent) -> void:
	_handle_input(_event)

func _unhandled_input(_event: InputEvent) -> void:
	_handle_input(_event) 

func _handle_input(_event: InputEvent) -> void:
	if _event is InputEventMouse:
		_handle_mouse_input(_event)
	elif _event is InputEventKey:
		_handle_keyboard_input(_event)
	

func _handle_mouse_input(_mouse_event:InputEventMouse) -> void:
	if _mouse_event is InputEventMouseButton:
		if ray_trace_caster != null:
			ray_trace_caster.cast_rays(_mouse_event)
		if Input.is_action_pressed("game_zoom_in"):
			_zoom_in()
		if Input.is_action_pressed("game_zoom_out"):
			_zoom_out()			
			
func _handle_keyboard_input(_keyboard_event:InputEventKey) -> void:
	previously_pressed_keyboard_buttons = currently_pressed_keyboard_buttons
	currently_pressed_keyboard_buttons = []
	
	if _keyboard_event.is_pressed():
			currently_pressed_keyboard_buttons.push_back(_keyboard_event)
		
	for previously_pressed_keyboard_button in previously_pressed_keyboard_buttons:
		var still_pressed = currently_pressed_keyboard_buttons.any( func(_event_key:InputEventKey): return _event_key.keycode == previously_pressed_keyboard_button.keycode)
		if !still_pressed:
			key_clicked.emit(_keyboard_event)
			
func handle_key_clicked(_key_event:InputEventKey):
	if self.input_handler != null: 		
		self.input_handler.on_key_clicked(_key_event)

func enable_ray_trace_casting():	
	if ray_trace_caster == null:
		ray_trace_caster = RayTraceCaster.new(self)

func disable_ray_trace_casting():
	if ray_trace_caster != null:
		ray_trace_caster = null
	
func _zoom_in() -> void:
	if _zoom_level > MIN_ZOOM_LEVEL:
		_zoom_level -= 1;
		_zoom();	
	
func _zoom_out() -> void:
	if _zoom_level < MAX_ZOOM_LEVEL:
		_zoom_level += 1;	
		_zoom();
		
func _zoom() -> void:		
	if camera == null: return
	camera.position.z = INITIAL_POSITION.y + (_zoom_level * ZOOM_STEP)	
