extends Object
class_name IPathFindingNode

func country_links_supply_for_faction(_country_id:int, _faction:Enum.Faction) -> bool:
	var country_state:CountryState = GameManager.game_state.country_state_by_id[_country_id];
	if !country_state.occupying_factions.has(_faction):
		return false	
	return true;

func are_countries_connected_connected_through_harbor(_country_id_1:int, _country_id_2:int, _faction:Enum.Faction) -> bool:
	var country_state_1:CountryState = GameManager.game_state.country_state_by_id[_country_id_1];
	var country_state_2:CountryState = GameManager.game_state.country_state_by_id[_country_id_2];
	
	for country_state in GameManager.game_state.country_states:
		if country_state.is_harbor_for(country_state_1, country_state_2) && country_state.occupying_factions.has(_faction):
			return true
			
	return false;
