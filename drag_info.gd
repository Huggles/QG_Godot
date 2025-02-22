class_name DragInfo

signal drag_completed(data:DragInfo)

var source: Control = null
var destination: Control = null

var preview: Control

func _init(_source: Control, _preview: Control):
	self.source = _source	
	self.preview = _preview
	self.preview.tree_exiting.connect(_on_tree_exiting)

func _on_tree_exiting()->void:
	drag_completed.emit(self)
