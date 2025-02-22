extends Node3D
class_name UnitScene

const ARMY_MESH_PATH  = "res://assets/meshes/game_elements/Army.obj" 
const NAVY_MESH_PATH  = "res://assets/meshes/game_elements/Navy.obj" 

static var unit_scene = preload("res://scenes/Units/Unit.tscn")

var unit_state:UnitState

@onready var clickable_sprite_node:ClickableSprite3D = %ClickableSprite3D

signal UNIT_CLICKED

var IS_ARMY:
	get: return unit_type == Enum.UnitType.ARMY
var IS_NAVY:
	get: return unit_type == Enum.UnitType.NAVY

	
var unit_type:Enum.UnitType:	
	get: return unit_state.type

var faction:String:	
	get: return unit_state.faction
var faction_data:FactionData:	
	get: return Globals.faction_data_map[faction]	
var country_state:CountryState:
	get: return unit_state.country_state
	


static func spawn_unit(_unit_state:UnitState) -> Node3D:
	var unit_scene_instance:UnitScene = unit_scene.instantiate()
	unit_scene_instance.unit_state = _unit_state;
	var _faction_data:FactionData = Globals.faction_data_map[_unit_state.faction]
	unit_scene_instance.name = str(_faction_data.name) + "_" + Enum.UnitType.keys()[_unit_state.type]	
	_unit_state.BEFORE_UNIT_DEPLOYED_TO_COUNTRY.connect(unit_scene_instance.on_before_unit_deployed_to_country)
	_unit_state.BEFORE_UNIT_REMOVED_FROM_COUNTRY.connect(unit_scene_instance.on_before_unit_removed_from_country)
	_unit_state.AFTER_UNIT_DEPLOYED_TO_COUNTRY.connect(unit_scene_instance.on_after_unit_deployed_to_country)
	_unit_state.AFTER_UNIT_REMOVED_FROM_COUNTRY.connect(unit_scene_instance.on_after_unit_removed_from_country)
	_unit_state.UNIT_BECOMES_CLICKABLE.connect(unit_scene_instance.on_become_clickable)
	_unit_state.UNIT_BECOMES_UNCLICKABLE.connect(unit_scene_instance.on_become_unclickable)
	return unit_scene_instance

	
func _ready():
	_set_mesh()
	_set_color()
	clickable_sprite_node.identifier = str(unit_state.faction,unit_state.id)
	
	
func _set_mesh():	
	if IS_ARMY:		
		%MeshInstance3D.mesh = load(ARMY_MESH_PATH)
		%MeshInstance3D.scale = Vector3(10,10,10)
	elif IS_NAVY:		
		%MeshInstance3D.mesh = load(NAVY_MESH_PATH)
		%MeshInstance3D.scale = Vector3(20,20,20)
		
func _set_color():
	var shader_material:ShaderMaterial = load("res://assets/materials/unit_shader_material.tres").duplicate()
	shader_material.set("shader_parameter/unit_color",faction_data.color() );
	%MeshInstance3D.material_override = shader_material
	
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
	
func on_before_unit_deployed_to_country() -> void:	
	print("on_before_unit_deployed_to_country")
	
	return
	
func on_after_unit_deployed_to_country() -> void:	
	print("on_after_unit_deployed_to_country")	
	country_state.node.add_unit(self)	
	return
	
func on_before_unit_removed_from_country() -> void:	
	print("on_before_unit_removed_from_country")	
	country_state.node.remove_unit(self)
	return
	
func on_after_unit_removed_from_country() -> void:	
	print("on_after_unit_removed_from_country")	
	return
