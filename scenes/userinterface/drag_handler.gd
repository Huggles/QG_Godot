extends Control
class_name DragHandler


@export var preview_scale:Vector2 = Vector2.ONE

var target_node:CanvasItem

var draggable = false
var is_inside_dropable = false
var dropped_on_target = false
var body_ref

var original_z_index: int

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	var parent_node = get_parent()
	if parent_node is Control:
		target_node = parent_node
		target_node.connect("mouse_entered", _on_mouse_entered)
		target_node.connect("mouse_exited", _on_mouse_exited)
		add_to_group("DRAGGABLE")
		#print("connected")		
	else:
		printerr("Could not determine draggable node")

	
func _on_mouse_entered():
	#print("mouse entered")
	target_node.scale = Vector2(1.05, 1.05)
	original_z_index = target_node.z_index
	target_node.z_index = 100

func _on_mouse_exited():
	#print("mouse exited")
	target_node.scale = Vector2(1, 1)
	target_node.z_index = original_z_index	
		
func _get_drag_data(_at_position: Vector2) -> Variant:		
	var drag_data = DragInfo.new(target_node, _create_item_preview())
	set_drag_preview(drag_data.preview)
	return drag_data

func _create_item_preview() -> Control:	
	var preview = target_node.duplicate()	
	preview.scale = preview_scale
	preview.pivot_offset = Vector2(-preview.size.x/2, -preview.size.y/2)
	return preview
	
func _can_drop_data(_at_position:Vector2, _data:Variant)->bool:
	if !_data is DragInfo: return false
	return true

func _drop_data(_at_position:Vector2, _data:Variant)->void:
	if !_data is DragInfo: return
	var drag_data := _data as DragInfo

	drag_data.destination = self
	if drag_data.source: drag_data.source.remove_item(drag_data.item)

	
