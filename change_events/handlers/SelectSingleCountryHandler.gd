class_name SelectSingleCountryHandler extends IGameEventHandler

var country_ids:Array[int]

func _init(_country_ids:Array[int], ) -> void:
	self.country_ids = _country_ids
	pass

func handle(_callback:Callable):	
	EventBusLocal.set_countries_clickable.emit(country_ids)
	var _selected_country_id:int = await EventBusLocal.country_clicked
	if _selected_country_id >= 0:
		EventBusLocal.set_all_countries_unclickable.emit()
		_callback.call(_selected_country_id)
			
			
	pass