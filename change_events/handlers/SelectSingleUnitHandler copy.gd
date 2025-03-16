class_name SelectSingleUnitHandler extends IGameEventHandler

var unit_ids:Array[int]

func _init(_unit_ids:Array[int], ) -> void:
	self.unit_ids = _unit_ids
	pass

func handle(_callback:Callable):	
	EventBusLocal.set_units_clickable.emit(unit_ids)
	var _selected_unit_id = await EventBusLocal.unit_clicked
	if _selected_unit_id >= 0:
		EventBusLocal.set_all_units_unclickable.emit()
		_callback.call(_selected_unit_id)
		