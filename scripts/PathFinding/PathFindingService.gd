extends Object
class_name PathFindingService

var a_star

func _init(_node_implementation:IPathFindingNode, _faction:Enum.Faction) -> void:
	a_star = AStar2D.new();	
	
	for country_state:CountryState in GameManager.game_state.country_states:
		if(_node_implementation.country_links_supply_for_faction(country_state.id, _faction)):
			var _world_position = country_state.static_country_data.WorldPositionCenter;
			a_star.add_point(country_state.id, Vector2.ONE, 1)			
				
	for country_state:CountryState in GameManager.game_state.country_states:
		for neighbor_country_state in country_state.neighbor_country_states:						
			if a_star.get_point_ids().has(country_state.id) && a_star.get_point_ids().has(neighbor_country_state.id):
				a_star.connect_points(country_state.id, neighbor_country_state.id, true)
	

func calculate_path(from_country_id:int, to_country_id:int) -> bool:
	var path = a_star.get_id_path(from_country_id, to_country_id, false) 
	if path.size() == 0:
		printerr(str("Could not find path from: ", CountryState.for_id(from_country_id).clabel," to ", CountryState.for_id(to_country_id).clabel))
		return false
	
	for path_id in path:
		print(GameManager.game_state.country_state_by_id[path_id].clabel)
	
	return true
