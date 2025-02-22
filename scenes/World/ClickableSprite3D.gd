extends Sprite3D
class_name ClickableSprite3D

var mouse_over := false

var identifier:String
var clickable_texture:Texture2D:
	set(value): texture = value
var image : Image

var selectable_color:Color = Color.WHITE	
var hover_color:Color = Color.GREEN

@onready var clickable_sprite_area := %ClickableSpriteArea
@onready var collision_shape := %CollisionShape3D

signal mouse_enter
signal mouse_exit
signal mouse_enter_opaque
signal mouse_exit_opaque
signal mouse_left_click_opaque
signal mouse_left_double_click_opaque

static var clickable_sprite_scene_file = preload("res://scenes/clickable_sprite_3d.tscn")
static func create_with_texture(_texture:Texture2D) -> ClickableSprite3D:
	var clickableSpriteScene:ClickableSprite3D = clickable_sprite_scene_file.instantiate();
	clickableSpriteScene.clickable_texture = _texture;			
	return clickableSpriteScene

func _ready():			
	collision_shape.shape = collision_shape.shape.duplicate()			
	set_glow_color(selectable_color)
	disable()
	_set_collision_shape()


	
	
	

func enable():
	visible = true
	collision_shape.disabled = false

func disable():	
	self.visible = false
	collision_shape.disabled = true

func _on_texture_changed() -> void:		
	self.get_material_override().set_shader_parameter("texture", texture)		
	image = texture.get_image()
	if image:		
		if image.is_compressed():
			image.decompress()		
		_set_collision_shape()
		
func _set_collision_shape():
	if collision_shape:		
		collision_shape.shape.size.x = texture.get_width() * pixel_size
		collision_shape.shape.size.y = texture.get_height() * pixel_size
		
func is_pixel_opaque(input_position: Vector3) -> bool:
	if image:
		var pixel_position = (input_position - global_position) / (pixel_size*scale)
		var texture_local_x = pixel_position.x + (texture.get_width() / 2.0)
		var texture_local_y = pixel_position.z + (texture.get_height() / 2.0)		
		#print("-------------------------")		
		#print(input_position)
		#print(global_position)
		#print(input_position - global_position)
		#print(pixel_position)
		#print(texture_local_x)
		#print(texture_local_y)
		
		if texture_local_x < 0 || texture_local_y < 0 || texture_local_x > image.get_size().x || texture_local_y > image.get_size().y:
			return false		
		var pixel = image.get_pixel(texture_local_x, texture_local_y);
		#print(pixel)
		
		return pixel.a > 0
	else:
		return false

func set_glow_color(color:Color):
	self.get_material_override().set_shader_parameter("glow_color", color)	

func _on_clickable_sprite_area_mouse_enter(_raycast_handler:RayTraceHandler) -> void:
	mouse_enter.emit(self);

func _on_clickable_sprite_area_mouse_exit(_raycast_handler:RayTraceHandler) -> void:
	mouse_exit.emit(self);

func _on_clickable_sprite_area_mouse_enter_opaque(_raycast_handler:RayTraceHandler) -> void:
	set_glow_color(hover_color)
	mouse_enter_opaque.emit(self)	

func _on_clickable_sprite_area_mouse_exit_opaque(_raycast_handler:RayTraceHandler) -> void:
	set_glow_color(selectable_color)
	mouse_exit_opaque.emit(self)

func _on_clickable_sprite_area_mouse_single_clicked_opaque_area(_raycast_handler:RayTraceHandler) -> void:
	mouse_left_click_opaque.emit(self)

func _on_clickable_sprite_area_mouse_double_clicked_opaque_area(_raycast_handler:RayTraceHandler) -> void:
	mouse_left_double_click_opaque.emit(self)
