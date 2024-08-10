extends Node3D
class_name UnitSceneBase

const ARMY_MESH_PATH  = "res://assets/meshes/game_elements/Army.obj" 
const NAVY_MESH_PATH  = "res://assets/meshes/game_elements/Navy.obj" 

static var unit_scene = preload("res://scenes/Units/Unit.tscn")


var IS_ARMY:
	get: return unit_type == Enum.UnitType.ARMY
var IS_NAVY:
	get: return unit_type == Enum.UnitType.NAVY


@export var unit_type:Enum.UnitType:
	set(value): 
		unit_type = value
		_set_mesh()
		
var faction:FactionData:	
	set(value): 		
		faction = value
		_set_color()
		
@export var faction_data_string:String:
	get: return faction.stringify() if faction else faction_data_string
	set(string): 
		faction = FactionData.new(string)

static func spawn_unit(faction_1:FactionData, unit_type_1:Enum.UnitType) -> Node3D:
	var unit_scene_instance:UnitSceneBase = unit_scene.instantiate()
	unit_scene_instance.name = str(faction_1.name) + "_" + Enum.UnitType.keys()[unit_type_1]
	unit_scene_instance.unit_type = unit_type_1
	unit_scene_instance.faction = faction_1
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
	shader_material.set("shader_parameter/unit_color",faction.color() );
	%MeshInstance3D.material_override = shader_material
