class_name RayTraceHandler
extends Area3D

var mouse_previously_over_opaque := false
var mouse_currently_over_opaque := false

signal mouse_enter
signal mouse_exit
signal mouse_enter_opaque
signal mouse_exit_opaque
signal mouse_single_clicked_opaque_area
signal mouse_double_clicked_opaque_area

var clickable_sprite:ClickableSprite3D:
	get: return self.get_parent()

func _ready():
	pass		

func on_start_hit(_camera: Node, _event: InputEvent, _input_position:Vector3, _normal: Vector3):
	mouse_enter.emit(self)	
	pass

func on_stop_hit(_camera: Node, _event: InputEvent, _input_position:Vector3, _normal: Vector3):
	mouse_exit.emit(self)
	mouse_exit_opaque.emit(self)		
	mouse_previously_over_opaque = false
	pass
	
func on_hitting(_camera: Node, _event: InputEvent, _input_position:Vector3, _normal: Vector3) -> void:		
	mouse_currently_over_opaque = clickable_sprite.is_pixel_opaque(_input_position)				
	if mouse_previously_over_opaque == false && mouse_currently_over_opaque == true:
		print(str("mouse_enter_opaque: ",clickable_sprite.identifier))		
		mouse_enter_opaque.emit(self)	
	elif mouse_previously_over_opaque == true && mouse_currently_over_opaque == false:
		print(str("mouse_exit_opaque: ",clickable_sprite.identifier))		
		mouse_exit_opaque.emit(self)		
		
	if _event is InputEventMouseButton:	
		var mouse_button_event:InputEventMouseButton = _event
		if mouse_currently_over_opaque:
			if mouse_button_event.pressed == true && mouse_button_event.button_index == 1:
				mouse_single_clicked_opaque_area.emit(self)
			if mouse_button_event.double_click == true:
				mouse_single_clicked_opaque_area.emit(self)
		if !mouse_currently_over_opaque:
			if mouse_button_event.pressed == true && mouse_button_event.button_index == 1:
				pass
			if mouse_button_event.double_click == true:
				pass
				
	mouse_previously_over_opaque = mouse_currently_over_opaque
	

	
