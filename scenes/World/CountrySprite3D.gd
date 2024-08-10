class_name CountrySprite3D
extends Sprite3D

var mouse_over := false

var country_label: String
var image : Image

@onready var clickable_sprite_area := %ClickableSpriteArea
@onready var collision_shape := %CollisionShape3D



func _ready():
	collision_shape.shape = collision_shape.shape.duplicate()
	_on_texture_changed()
	clickable_sprite_area.connect("mouse_entered_opaque", _handle_mouse_entered_opaque)
	clickable_sprite_area.connect("mouse_exited_opaque", _handle_mouse_exited_opaque)
	
	
func _handle_mouse_entered_opaque():
	print("Started Hovering over: " + country_label)
	pass

func _handle_mouse_exited_opaque():
	print("Stopped Hovering over: " + country_label)
	pass

func _on_texture_changed() -> void:
	image = texture.get_image()
	if image:		
		if image.is_compressed():
			image.decompress()
		
		if collision_shape:		
			collision_shape.shape.size.x = texture.get_width() * pixel_size
			collision_shape.shape.size.y = texture.get_height() * pixel_size
		
func is_pixel_opaque(input_position: Vector3) -> bool:
	var pixel_position = (input_position - global_position) / (pixel_size*scale)
	var texture_local_x = pixel_position.x + (texture.get_width() / 2.0)
	var texture_local_y = texture.get_height() - (pixel_position.y + texture.get_height() / 2.0)
	
	return image.get_pixel(texture_local_x, texture_local_y).a
	
