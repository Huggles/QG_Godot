extends Object
class_name PathFindingService

static func calculate_path(_node_implementation:IPathFindingNode, _faction:Enum.Faction, from_country_id:int, to_country_id:int) -> bool:
	var astar = AStar2D.new();
	
	for country_state:CountryState in GameManager.game_state.country_states:
		if(_node_implementation.country_links_supply_for_faction(country_state.id, _faction)):
			var _world_position = country_state.static_country_data.WorldPositionCenter;
			astar.add_point(country_state.id, Vector2.ONE, 1)			
				
	for country_state:CountryState in GameManager.game_state.country_states:
		for neighbor_country_state in country_state.neighbor_country_states:						
			if astar.get_point_ids().has(country_state.id) && astar.get_point_ids().has(neighbor_country_state.id):
				astar.connect_points(country_state.id, neighbor_country_state.id, true)
		
	var path = astar.get_id_path(from_country_id, to_country_id, false) 
	if path.size() == 0:
		printerr(str("Could not find path from: ", GameManager.game_state.country_state_by_id[from_country_id].clabel," to ", GameManager.game_state.country_state_by_id[to_country_id].clabel))
	
	for path_id:int in path:
		print(GameManager.game_state.country_state_by_id[path_id].clabel)
	
	return false
