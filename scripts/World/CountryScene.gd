class_name CountryScene
extends Node3D

var country_state:CountryState
var static_country_data:CountryData:
	get: return self.country_state.static_country_data

const unit_positions = [
	Vector3(0,0,0), 
	Vector3(-100,0,0), 	
	Vector3(100,0,0)]

var unit_scenes:Array[UnitScene]

@onready var unit_container_node:Node3D = %UnitContainer
@onready var clickable_sprite_node:ClickableSprite3D = %ClickableSprite3D

signal COUNTRY_CLICKED

static var country_scene = preload("res://scenes/World/Country.tscn")

static func spawn_country(_country_state:CountryState) -> CountryScene:
	var _country_scene_instance:CountryScene = country_scene.instantiate()	
	print(str("spawn country: ", _country_state.clabel))
	_country_scene_instance.country_state = _country_state		
	_country_scene_instance.name = _country_state.name 		
	_country_state.COUNTRY_BECOMES_CLICKABLE.connect(_country_scene_instance.on_become_clickable)
	_country_state.COUNTRY_BECOMES_UNCLICKABLE.connect(_country_scene_instance.on_become_unclickable)	
	return _country_scene_instance
	
func _ready() -> void:
	if static_country_data.texture:					
		_apply_texture()		
	self._set_clickable(false)
	clickable_sprite_node.identifier = self.country_state.clabel
		
func _apply_texture():	
	clickable_sprite_node.clickable_texture = static_country_data.texture

func add_unit(unit:UnitScene):
	unit.get_parent().remove_child(unit)	
	%UnitContainer.add_child(unit)
	unit.position = unit_positions[unit_scenes.size()]	
	unit_scenes.push_back(unit)
	
func remove_unit(unit:UnitScene):	
	%UnitContainer.remove_child(unit)
	NodeUtilities.units_node.add_child(unit)
	unit.position = Vector3.ZERO	
	unit_scenes.erase(unit)
	
func on_become_clickable() -> void:
	_set_clickable(true)

func on_become_unclickable() -> void:
	_set_clickable(false)

func _set_clickable(clickable:bool) -> void:		
	if clickable_sprite_node != null:
		if clickable:
			clickable_sprite_node.enable()
		else:
			clickable_sprite_node.disable()

func _on_clickable_sprite_3d_mouse_enter_opaque(_sprite:ClickableSprite3D) -> void:	
	pass # Replace with function body.
