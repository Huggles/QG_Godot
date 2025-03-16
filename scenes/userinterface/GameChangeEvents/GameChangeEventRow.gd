class_name GameChangeEventRow extends Control



const SHOW_DURATION:float = 3.0

var label_node:Label:
	get: return %Label


func _ready() -> void:
	self.show_row()
	
func show_row():	
	%MarginContainer.visible = true;
	#self.set_hide_timer();
	
func set_hide_timer():	
	get_tree().create_timer(SHOW_DURATION).timeout.connect(
		func():			
			%MarginContainer.visible = false
			)	


func _on_label_mouse_entered() -> void:
	self.show_row()


func _on_mouse_entered() -> void:
	self.show_row();
