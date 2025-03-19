class_name CountryScene
extends Node3D

var country_state:CountryState
var static_country_data:CountryData:
	get: return self.country_state.static_country_data	
var straight_state:StraightState:
	get: return country_state.straight_state

var unit_scene_1:UnitScene
var unit_scene_2:UnitScene
var unit_scene_3:UnitScene

var unit_container_node:Node3D:
	get: return %UnitContainer
var clickable_sprite_node:ClickableSprite3D:
	get: return %ClickableSprite3D
var supply_star_sprite:Sprite3D:
	get: return %SupplyStarSprite3D
var straight_sprite_node:ClickableSprite3D:
	get: return %StraightSprite3D	

var clickable:bool
var click_callback:Callable

static var country_scene = preload("res://scenes/World/Country.tscn")

static func spawn_country(_country_state:CountryState) -> CountryScene:
	var _country_scene_instance:CountryScene = country_scene.instantiate()		
	_country_scene_instance.country_state = _country_state		
	_country_scene_instance.name = _country_state.name 			
	return _country_scene_instance 
	
func _ready() -> void:
	if static_country_data.texture:							
		_apply_texture()		
	self.set_unclickable()
	clickable_sprite_node.identifier = self.country_state.clabel
	
	if country_state.is_supply:
		show_supply_star()
	else:
		hide_supply_star()
	straight_state.on_ready()
	_set_debug_unit_position(false)
	
		
func _set_debug_unit_position(_visible:bool):
	%DEBUG_UnitPosition_Sprite1.position = static_country_data.unit_transform_data.position_1.position_vector3
	%DEBUG_UnitPosition_Sprite1.modulate = Color.GREEN	
	%DEBUG_UnitPosition_Sprite1.visible = _visible
	
	%DEBUG_UnitPosition_Sprite2.position = static_country_data.unit_transform_data.position_2.position_vector3
	%DEBUG_UnitPosition_Sprite2.modulate = Color.BLUE
	%DEBUG_UnitPosition_Sprite2.visible = _visible
	
	%DEBUG_UnitPosition_Sprite3.position = static_country_data.unit_transform_data.position_3.position_vector3
	%DEBUG_UnitPosition_Sprite3.modulate = Color.RED
	%DEBUG_UnitPosition_Sprite3.visible = _visible

func _apply_texture():		
	clickable_sprite_node.clickable_texture = static_country_data.texture

func add_unit(_unit_scene:UnitScene):
	var _position:int = _set_unit_on_available_position(_unit_scene)
	if _position > 0:
		_unit_scene.get_parent().remove_child(_unit_scene)	
		unit_container_node.add_child(_unit_scene)
		if country_state.static_country_data.unit_transform_data != null:
			var unit_transform_data:UnitTransformData = country_state.static_country_data.unit_transform_data	
			var key = str("position_",_position)
			var _transform = unit_transform_data.get(key)	
			_unit_scene.position = Vector3(_transform.x_position, _transform.y_position, _transform.z_position)
			_unit_scene.scale = Vector3(_transform.scale,_transform.scale,_transform.scale)
		
func _get_unit_position(_unit_scene:UnitScene) -> int:
	if self.unit_scene_1 == _unit_scene: return 1
	if self.unit_scene_2 == _unit_scene: return 2
	if self.unit_scene_3 == _unit_scene: return 3
	else: return -1

func _set_unit_on_available_position(_unit_scene:UnitScene) -> int:
	if self.unit_scene_1 == null: 
		self.unit_scene_1 = _unit_scene
		return 1
	if self.unit_scene_2 == null: 
		self.unit_scene_2 = _unit_scene
		return 2
	if self.unit_scene_3 == null: 
		self.unit_scene_3 = _unit_scene
		return 3
	else:
		return -1
	
func remove_unit(_unit_scene:UnitScene):	
	var _position:int = _get_unit_position(_unit_scene)
	if _position > 0:		
		unit_container_node.remove_child(_unit_scene)
		NodeUtilities.units_node.add_child(_unit_scene)
		_unit_scene.position = Vector3.ZERO	
		self.set(str("unit_scene_", _position), null)

func set_clickable(_callback:Callable) -> void:		
	self.clickable = true
	self.click_callback = _callback
	clickable_sprite_node.enable()
	
func set_unclickable():
	self.clickable = false
	self.click_callback = Callable()
	clickable_sprite_node.disable()
	
func show_supply_star() -> void:		
	supply_star_sprite.visible = true
	if country_state.static_country_data.supply_star_transform_data != null:
		var supply_star_data = country_state.static_country_data.supply_star_transform_data
		supply_star_sprite.position = Vector3(supply_star_data.x_position, supply_star_data.y_position, supply_star_data.z_position)
		supply_star_sprite.scale = Vector3(supply_star_data.scale,supply_star_data.scale,supply_star_data.scale)

func hide_supply_star() -> void:		
	supply_star_sprite.visible = false
	

func _on_clickable_sprite_3d_mouse_enter_opaque(_sprite:ClickableSprite3D) -> void:	
	pass # Replace with function body.

func _on_clickable_sprite_3d_mouse_left_click_opaque(_clickable_sprite:ClickableSprite3D) -> void:
	print("_on_clickable_sprite_3d_mouse_left_click_opaque")
	if clickable == true && click_callback != null:
		click_callback.call(self)
	
