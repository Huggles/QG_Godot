extends Node3D
class_name UnitScene

const ARMY_SPRITE_PATH  = "res://assets/textures/Units/QGArmy.png"
const ARMY_SPRITE  = preload(ARMY_SPRITE_PATH)
const NAVY_SPRITE_PATH  = "res://assets/textures/Units/QGNavy.png"
const NAVY_SPRITE  = preload(NAVY_SPRITE_PATH)
static var unit_scene = preload("res://scenes/Units/Unit.tscn")

var unit_state:UnitState

var unit_sprite_node:Sprite3D:
	get: return %UnitSprite3D
var clickable_sprite_node:ClickableSprite3D:
	get: return %ClickableSprite3D
var out_of_supply_node:Sprite3D:
	get: return %OutOfSupplyIcon
var debug_label_node:Label3D:
	get: return %DebugLabel3D

var clickable:bool
var click_callback:Callable

var normal_shader_material:ShaderMaterial = preload("res://assets/materials/unit_shader_material.tres").duplicate()
var out_of_supply_shader_material:ShaderMaterial = preload("res://assets/materials/UnitOutOfSupplyShaderMaterial.tres").duplicate()

signal UNIT_CLICKED
signal UNIT_DOUBLE_CLICKED

var IS_ARMY:
	get: return unit_type == Enum.UnitType.ARMY
var IS_NAVY:
	get: return unit_type == Enum.UnitType.NAVY
	
var unit_type:Enum.UnitType:	
	get: return unit_state.type
var faction:String:	
	get: return unit_state.faction
var faction_data:FactionData:	
	get: return StaticGameData.faction_data_map[faction]	
var country_state:CountryState:
	get: return unit_state.country_state

static func spawn_unit(_unit_state:UnitState) -> Node3D:
	var unit_scene_instance:UnitScene = unit_scene.instantiate()
	unit_scene_instance.unit_state = _unit_state;
	var _faction_data:FactionData = StaticGameData.faction_data_map[_unit_state.faction]
	unit_scene_instance.name = str(_faction_data.name) + "_" + Enum.UnitType.keys()[_unit_state.type]	
	_unit_state.BEFORE_UNIT_DEPLOYED_TO_COUNTRY.connect(unit_scene_instance.on_before_unit_deployed_to_country)
	_unit_state.BEFORE_UNIT_REMOVED_FROM_COUNTRY.connect(unit_scene_instance.on_before_unit_removed_from_country)
	_unit_state.AFTER_UNIT_DEPLOYED_TO_COUNTRY.connect(unit_scene_instance.on_after_unit_deployed_to_country)
	_unit_state.AFTER_UNIT_REMOVED_FROM_COUNTRY.connect(unit_scene_instance.on_after_unit_removed_from_country)	
	return unit_scene_instance
	
func _ready():
	_set_sprite()
	
	debug_label_node.visible = false
	clickable_sprite_node.identifier = str(unit_state.faction,unit_state.id)	
	if unit_state.country_id >= 0 && !unit_state.in_supply:
		show_out_of_supply()
	
func _set_sprite():		
	if IS_ARMY:
		unit_sprite_node.texture = ARMY_SPRITE
	elif IS_NAVY:		
		unit_sprite_node.texture = NAVY_SPRITE
		
	unit_sprite_node.modulate = faction_data.color()		
	unit_sprite_node.sorting_offset = 50 + faction_data.faction_enum
	

func set_clickable(_callback:Callable) -> void:		
	self.clickable = true
	self.click_callback = _callback
	self.hide_out_of_supply()
	clickable_sprite_node.enable()
	
func set_unclickable():
	self.clickable = false
	self.show_out_of_supply()
	clickable_sprite_node.disable()	

func show_out_of_supply():			
	%OutOfSupplyIcon.visible = true

func hide_out_of_supply():
	%OutOfSupplyIcon.visible = false	
	
func on_before_unit_deployed_to_country() -> void:	
	return
	
func on_after_unit_deployed_to_country() -> void:		
	country_state.node.add_unit(self)	
	var tween = get_tree().create_tween()	
	tween.tween_property(unit_sprite_node, "pixel_size", 0.04, 0.2)
	tween.tween_property(unit_sprite_node, "pixel_size", 0.02, 0.2)
	await tween.finished
	return
	
func on_before_unit_removed_from_country(_unit_id:int, _country_id:int) -> void:		
	var tween = get_tree().create_tween()	
	tween.tween_property(unit_sprite_node, "pixel_size", 0.025, 0.2)
	tween.tween_property(unit_sprite_node, "pixel_size", 0.00, 0.4)
	await tween.finished
	CountryState.for_id(_country_id).node.remove_unit(self)
	return
	
func on_after_unit_removed_from_country() -> void:		
	return

func _on_clickable_sprite_3d_mouse_left_click_opaque(_clickable_sprite:ClickableSprite3D) -> void:
	if clickable == true && click_callback != null:
		click_callback.call(self)
