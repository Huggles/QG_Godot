class_name GameChangeEventContainer extends VBoxContainer

const CHANGE_EVENT_ROW_SCENE_RESOURCE = preload("res://scenes/userinterface/GameChangeEvents/GameChangeEventRow.tscn")

var change_event_row_scene
var _events_to_show:Array[GameChangeEvent] =[]

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	change_event_row_scene = CHANGE_EVENT_ROW_SCENE_RESOURCE.instantiate()
	_init_events()
	EventBusLocal.game_change_event_occurred.connect(
		func(change_event):
			_events_to_show.push_front(change_event)
			#_add_event(change_event, _events_to_show.size())
			_init_events()
			)
	
	
func _init_events():	
	for child in self.get_children():
		self.remove_child(child)		
	var counter = 0
	if GameManager.game_state && GameManager.game_state.game_change_events:
		_events_to_show = GameManager.game_state.game_change_events.duplicate();
		_events_to_show.reverse()
		for game_change_event:GameChangeEvent in _events_to_show:			
			_add_event(game_change_event,counter)
			counter+=1
			
func _add_event(game_change_event:GameChangeEvent, index:int):
	var row_control = change_event_row_scene.duplicate()	
	var event_class = game_change_event.get_script().get_global_name()			
	var row_name = str(event_class,index)			
	var row_instance:GameChangeEventRow = row_control.duplicate()			
	row_instance.name = row_name
	row_instance.label_node.text = game_change_event.display_text()
	
	self.add_child(row_instance)
			
			


			
		
			
	
			
	
