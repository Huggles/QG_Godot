class_name InputManager extends Node3D

const INITIAL_POSITION:Vector3 = Vector3(0,1500,0)
const ZOOM_STEP:float = 100
const MIN_ZOOM_LEVEL:float = -10
const MAX_ZOOM_LEVEL:float = 10
const PLAY_CARD_KEYS = [KEY_0,KEY_1,KEY_2,KEY_3,KEY_4,KEY_5,KEY_6]


@export var camera_speed:float = 10;
var camera:PlayerCamera3D:
	get: return %Camera3D

var ray_trace_caster:RayTraceCaster
var _zoom_level:int = 0;

var input_map:Dictionary = {
	"none" : _ignore_input,
	"debug" : _handle_debug_input,
	"play_card" : _handle_play_card_input
}
var active_input_key:String = "general"

var currently_pressed_mouse_buttons:Array[InputEventMouseButton]
var previously_pressed_mouse_buttons:Array[InputEventMouseButton]
var currently_pressed_keyboard_buttons:Array[InputEventKey]
var previously_pressed_keyboard_buttons:Array[InputEventKey]

func set_card_input_active():
	active_input_key = "play_card"

func set_debug_input_active():
	active_input_key = "debug"

func set_no_input_active():
	active_input_key = "none"

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
	
	var zoom_multiplier = 10 - _zoom_level; #Move faster when zoomed in.
	var x_delta:float = (-input_left + input_right) * camera_speed;
	var z_delta:float = (-input_up + input_down) * camera_speed;
	
	var delta:Vector3 = Vector3(x_delta, 0, z_delta) * clamp(zoom_multiplier, 3,20)
	camera.position += delta
	return

func _input(_event: InputEvent) -> void:
	previously_pressed_keyboard_buttons = currently_pressed_keyboard_buttons
	currently_pressed_keyboard_buttons = []
	
	previously_pressed_mouse_buttons = currently_pressed_mouse_buttons
	currently_pressed_mouse_buttons = []
	
	if _event is InputEventKey:			
		if _event.is_pressed():
			currently_pressed_keyboard_buttons.push_back(_event)
		
	if _event is InputEventMouseButton:	
		if _event.is_pressed():
			currently_pressed_mouse_buttons.push_back(_event)		
		
		
	for previously_pressed_keyboard_button in previously_pressed_keyboard_buttons:
		var still_pressed = currently_pressed_keyboard_buttons.any( func(_event_key:InputEventKey): return _event_key.keycode == previously_pressed_keyboard_button.keycode)
		if !still_pressed:
			_handle_keyboard_key_clicked(previously_pressed_keyboard_button)			

func _handle_keyboard_key_clicked(_keyboard_event:InputEventKey):
	if _keyboard_event.keycode == KEY_9:
		active_input_key = "general" if active_input_key == "play_card" else "play_card"
		DebugUtilities.print_peer(str("Switched active key map: ", active_input_key))
	else: 		
		input_map.get(active_input_key).call(_keyboard_event)
		 
		
	
func _handle_debug_input(_keyboard_event:InputEventKey) -> void:
	if _keyboard_event.keycode == KEY_1:
		GameManager.game_flow.progress_game()
		
	if _keyboard_event.keycode == KEY_2:
		var _faction_state:FactionState = GameManager.game_state.faction_states[GameManager.game_flow.current_faction]
		_faction_state.deck[0].execute_card()		
		
	if _keyboard_event.keycode == KEY_3:
		var _faction_state:FactionState = GameManager.game_state.faction_states[GameManager.game_flow.current_faction]
		_faction_state.deck[1].execute_card()
			
	if _keyboard_event.keycode == KEY_4:
		GameStateUtilities.recalculate_supply()


func _handle_play_card_input(_keyboard_event:InputEventKey) -> void:
	for key in PLAY_CARD_KEYS:		
		if _keyboard_event.keycode == key:
			var index = PLAY_CARD_KEYS.find(key)
			GameManager.game_flow.current_faction_state.play_card_at_hand_index(index, func():)
			return

func _ignore_input(_keyboard_event:InputEventKey) -> void:
	pass

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
	
func _zoom_in() -> void:
	print('zoom in')	
	if _zoom_level > MIN_ZOOM_LEVEL:
		_zoom_level -= 1;
		_zoom();	
	
func _zoom_out() -> void:
	print('zoom out')	
	if _zoom_level < MAX_ZOOM_LEVEL:
		_zoom_level += 1;	
		_zoom();
		
func _zoom() -> void:		
	if camera == null: return
	camera.position.y = INITIAL_POSITION.y + (_zoom_level * ZOOM_STEP)	
