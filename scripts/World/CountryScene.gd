class_name CountryScene
extends Node3D

var country_data: CountryData

const unit_positions = [Vector3(-100,0,0), Vector3(0,0,0), Vector3(100,0,0)]

var units:Array[UnitSceneBase]





@onready var clickable_sprite_node:Sprite3D = %ClickableSprite3D
var _clickable:bool
var clickable:bool:
	get: return _clickable
	set(value):
		_clickable = value
		_set_clickable(value)

static var country_scene = preload("res://scenes/World/Country.tscn")

static func spawn_country(country_data_1:CountryData) -> CountryScene:
	var country_scene_instance:CountryScene = country_scene.instantiate()
	country_scene_instance.country_data = country_data_1	
	country_scene_instance._initialize_components()
	country_scene_instance.name = country_data_1.name 	
	return country_scene_instance
	
func _initialize_components():			
	if country_data.texture:					
		_apply_texture()		
		_set_clickable(false)
	
func _apply_texture():
	%ClickableSprite3D.texture = country_data.texture
	%ClickableSprite3D.country_label = country_data.clabel
	%ClickableSprite3D.sorting_offset = country_data.number

func add_unit(unit:UnitSceneBase):	
	unit.global_position = self.position + unit_positions[units.size()]	
	units.push_back(unit)
	
func remove_unit(unit:UnitSceneBase):	
	unit.global_position = self.position + unit_positions[units.size()]	
	units.push_back(unit)

func _set_clickable(c:bool) -> void:		
	%ClickableSprite3D.visible = c	
	%ClickableSprite3D/ClickableSpriteArea/CollisionShape3D.disabled = not c	
