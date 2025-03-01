class_name CountryScene
extends Node3D

var country_state:CountryState
var static_country_data:CountryData:
	get: return self.country_state.static_country_data


var unit_scenes:Array[UnitScene]

var supply_star_sprite:Sprite3D:
	get: return %SupplyStarSprite3D

@onready var unit_container_node:Node3D = %UnitContainer
@onready var clickable_sprite_node:ClickableSprite3D = %ClickableSprite3D

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
	show_supply_star()
		
func _apply_texture():	
	clickable_sprite_node.clickable_texture = static_country_data.texture

func add_unit(unit:UnitScene):
	unit.get_parent().remove_child(unit)	
	%UnitContainer.add_child(unit)
	unit_scenes.push_back(unit)
	if country_state.static_country_data.unit_transform_data != null:
		var unit_transform_data:UnitTransformData = country_state.static_country_data.unit_transform_data		
		unit.position = Vector3(unit_transform_data.position_1.x_position, unit_transform_data.position_1.y_position, unit_transform_data.position_1.z_position)
		unit.scale = Vector3(unit_transform_data.position_1.scale,unit_transform_data.position_1.scale,unit_transform_data.position_1.scale)
	
	
	
func remove_unit(unit:UnitScene):	
	%UnitContainer.remove_child(unit)
	NodeUtilities.units_node.add_child(unit)
	unit.position = Vector3.ZERO	
	unit_scenes.erase(unit)

func set_clickable(_callback:Callable) -> void:		
	self.clickable = true
	self.click_callback = _callback
	clickable_sprite_node.enable()
	
func set_unclickable():
	self.clickable = false
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
	if clickable == true && click_callback != null:
		click_callback.call(self)
	
