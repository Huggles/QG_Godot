class_name PathFindingService extends Object

var a_star:AStar2D

func _init(_node_implementation:IPathFindingNode, _faction:Enum.Faction) -> void:
	a_star = AStar2D.new();		
	for country_state:CountryState in GameManager.game_state.country_states:
		if(_node_implementation.country_links_supply_for_faction(country_state.id, _faction)):		
			a_star.add_point(country_state.id, Vector2.ONE, 1)
			
	for _country_id in a_star.get_point_ids():
		var _country_state:CountryState = CountryState.for_id(_country_id)
		for _connected_country_state:CountryState in _country_state.connected_countries(_faction):	
			if a_star.get_point_ids().has(_connected_country_state.id):
				a_star.connect_points(_country_state.id, _connected_country_state.id, true)

func calculate_path(from_country_id:int, to_country_id:int) -> bool:
	var path = a_star.get_id_path(from_country_id, to_country_id, false) 
	if path.size() == 0:
		#printerr(str("Could not find path from: ", CountryState.for_id(from_country_id).clabel," to ", CountryState.for_id(to_country_id).clabel))
		return false
	
	#for path_id in path:
		#print(GameManager.game_state.country_state_by_id[path_id].clabel)
	
	return true
