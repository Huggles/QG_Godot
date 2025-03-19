class_name VictoryPointStepHandlerDefault extends Object


var vp_turn_summary:VPTurnSummary 


func process_turn(_faction:Enum.Faction) -> VPTurnSummary:
	vp_turn_summary = VPTurnSummary.new(GameManager.game_flow.round)
	
	var _faction_state = GameManager.game_state.faction_state_for_enum(_faction)
	for _cs:CountryState in CountryState.for_ids(_faction_state.occupied_country_ids):
		if _cs.is_supply == true:	
			var score = max(3 - _cs.units.keys().size(), 1)
			vp_turn_summary.add_score(score, str("supply star on ", _cs.static_country_data.clabel))			
	_faction_state.score += vp_turn_summary.total_score
	GameManager.game_flow.victory_point_summaries[_faction].push_back(vp_turn_summary)
	return vp_turn_summary