class_name IPathFindingNode extends Object

func country_links_supply_for_faction(_country_id:int, _faction:Enum.Faction) -> bool:
	var country_state:CountryState = GameManager.game_state.country_state_by_id[_country_id];
	if !country_state.occupying_factions.has(_faction):
		return false	
	return true;
	
func connected_countries()-> Array[int]:
	
	return []
