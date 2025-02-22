extends Node3D
class_name UnitSceneBase

const ARMY_MESH_PATH  = "res://assets/meshes/game_elements/Army.obj" 
const NAVY_MESH_PATH  = "res://assets/meshes/game_elements/Navy.obj" 

static var unit_scene = preload("res://scenes/Units/Unit.tscn")

var unit_state:UnitState

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
	var unit_scene_instance:UnitSceneBase = unit_scene.instantiate()
	unit_scene_instance.unit_state = _unit_state;
	var faction_data:FactionData = Globals.faction_data_map[_unit_state.faction]
	unit_scene_instance.name = str(faction_data.name) + "_" + Enum.UnitType.keys()[_unit_state.type]	
	_unit_state.BEFORE_UNIT_DEPLOYED_TO_COUNTRY.connect(unit_scene_instance.on_before_unit_deployed_to_country)
	_unit_state.BEFORE_UNIT_REMOVED_FROM_COUNTRY.connect(unit_scene_instance.on_before_unit_removed_from_country)
	_unit_state.AFTER_UNIT_DEPLOYED_TO_COUNTRY.connect(unit_scene_instance.on_after_unit_deployed_to_country)
	_unit_state.AFTER_UNIT_REMOVED_FROM_COUNTRY.connect(unit_scene_instance.on_after_unit_removed_from_country)
	return unit_scene_instance

func _enter_tree() -> void:
	$UnitSynchronizer.set_multiplayer_authority(1)
	
func _ready():
	_set_mesh()
	_set_color()
	
func _set_mesh():	
	if IS_ARMY:		
		%MeshInstance3D.mesh = load(ARMY_MESH_PATH)
		%MeshInstance3D.scale = Vector3(2,2,2)
	elif IS_NAVY:		
		%MeshInstance3D.mesh = load(NAVY_MESH_PATH)
		%MeshInstance3D.scale = Vector3(4,4,4)
		
func _set_color():
	var shader_material:ShaderMaterial = load("res://assets/materials/unit_shader_material.tres").duplicate()
	shader_material.set("shader_parameter/unit_color",faction_data.color() );
	%MeshInstance3D.material_override = shader_material
	
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
