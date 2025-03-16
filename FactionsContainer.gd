class_name FactionsContainer extends Control


static var instance:FactionsContainer


var horizontal_container:HBoxContainer: 
	get: return %FactionsHorizontalContainer
var faction_info_nodes:Dictionary
var row_scene

const CHANGE_EVENT_ROW_SCENE_RESOURCE = preload("res://FactionInfoRow.tscn")

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	if FactionsContainer.instance == null:
		FactionsContainer.instance = self
		EventBusLocal.player_joined.connect(_on_player_joined)				
		EventBusLocal.player_left.connect(_on_player_left)
		row_scene = CHANGE_EVENT_ROW_SCENE_RESOURCE.instantiate()
		_init_child_elements();
	else: 
		assert(false, "Can only have 1 input PlayersContainer")

func _init_child_elements():
	for child in horizontal_container.get_children():
		horizontal_container.remove_child(child)	
	pass
	
	for _faction in Enum.Faction.values():	
		var _row_scene:FactionInfoRow = row_scene.duplicate()		
		_row_scene.faction = _faction		
		horizontal_container.add_child(_row_scene)
		faction_info_nodes[_faction] = _row_scene		

func _on_player_joined():
	_init_child_elements();
	pass

func _on_player_left():
	_init_child_elements();
	pass

func _faction_info_node_for_faction(_faction:Enum.Faction)->FactionInfoRow:
	return faction_info_nodes[_faction];
	
