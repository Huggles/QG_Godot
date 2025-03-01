extends Node3D
class_name UnitScene

const ARMY_MESH_PATH  = "res://assets/meshes/game_elements/Army.obj" 
const NAVY_MESH_PATH  = "res://assets/meshes/game_elements/Navy.obj" 

static var unit_scene = preload("res://scenes/Units/Unit.tscn")

var unit_state:UnitState

@onready var clickable_sprite_node:ClickableSprite3D = %ClickableSprite3D
@onready var out_of_supply_node:Sprite3D = %OutOfSupplyIcon

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
	_set_mesh()
	_set_color()
	clickable_sprite_node.identifier = str(unit_state.faction,unit_state.id)	
	if unit_state.country_id >= 0 && !unit_state.in_supply:
		show_out_of_supply()
	
func _set_mesh():	
	if IS_ARMY:		
		%MeshInstance3D.mesh = load(ARMY_MESH_PATH)
		%MeshInstance3D.scale = Vector3(10,10,10)
	elif IS_NAVY:		
		%MeshInstance3D.mesh = load(NAVY_MESH_PATH)
		%MeshInstance3D.scale = Vector3(20,20,20)
		%MeshInstance3D.rotation = Vector3(0,90,45)
		
func _set_color():		
	normal_shader_material.set_shader_parameter("unit_color", faction_data.color())	
	%MeshInstance3D.material_override = normal_shader_material

func set_clickable(_callback:Callable) -> void:		
	self.clickable = true
	self.click_callback = _callback
	clickable_sprite_node.enable()
	
func set_unclickable():
	self.clickable = false
	clickable_sprite_node.disable()
	

func show_out_of_supply():			
	%OutOfSupplyIcon.visible = true

func hide_out_of_supply():
	%OutOfSupplyIcon.visible = false
	
	
func on_before_unit_deployed_to_country() -> void:	
	return
	
func on_after_unit_deployed_to_country() -> void:		
	country_state.node.add_unit(self)	
	return
	
func on_before_unit_removed_from_country() -> void:		
	country_state.node.remove_unit(self)
	return
	
func on_after_unit_removed_from_country() -> void:		
	return


func _on_clickable_sprite_3d_mouse_left_click_opaque(_clickable_sprite:ClickableSprite3D) -> void:
	UNIT_CLICKED.emit(self)


func _on_clickable_sprite_3d_mouse_left_double_click_opaque(_clickable_sprite:ClickableSprite3D) -> void:
	UNIT_DOUBLE_CLICKED.emit(self)
