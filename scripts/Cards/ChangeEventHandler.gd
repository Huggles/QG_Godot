class_name ChangeEventHandler extends Node 

signal action_finished

var game_change_event:GameChangeEvent
var game_state:GameState:
	get: return GameManager.game_state

#Optional origin card for this change event
var origin_card_id:int = -1
var has_origin_card:bool:
	get: return origin_card_id > -1
var card_state:CardState:
	get: return CardState.for_id(origin_card_id) if has_origin_card else null

#Get the request order using the faction that last activated something
var last_activating_team:Enum.FactionTeam
var request_order:Array:
	get:
		var order = [] 
		order.append_array(StaticGameData.opponent_factions_for_team(last_activating_team))
		order.append_array(StaticGameData.factions_for_team(last_activating_team))         
		return order

func _init(_game_change_event:GameChangeEvent, _origin_card_id:int = -1):
	game_change_event = _game_change_event
	

func execute_change_event(_is_trigger:bool = true):	
	#Enrich
	print(str("ChangeEventHandler ", game_change_event.trace_text()))
	game_change_event.is_trigger = _is_trigger
	game_change_event.id = game_state._change_event_counter;
	game_state._change_event_counter += 1;	
	
	DebugUtilities.print_peer(str("pushed gce to back with id: ", game_change_event.id))
	GameManager.game_state.game_change_events.push_back(game_change_event)

	#await block
	await self.request_before_card_activation(game_change_event.id)
	if game_change_event.blocked == true:
		action_finished.emit()
		return
	
	DebugUtilities.print_peer(str("no further before cards found"))
	
	#execute
	EventBusLocal.game_change_event_before.emit(game_change_event.id)	
	game_change_event.apply_change()
	await GameManager.create_timer(200)
	EventBusLocal.game_change_event_after.emit(game_change_event.id)

static func execute_change_event_without_trigger(_game_change_event:GameChangeEvent) -> void:
	ChangeEventHandler.new(_game_change_event).execute_change_event(false)
	pass
		
###############################
# 	BEFORE CARD ACTIVATION    #
###############################
func request_before_card_activation(_gce_id:int):
	var _gce:GameChangeEvent = GameChangeEvent.for_id(_gce_id)
	last_activating_team = StaticGameData.faction_team_for_faction(_gce.triggering_faction)
	if _gce.is_trigger:
		for _faction in request_order:            
			var _activated_card:bool = await _request_before_card_activation_for_faction(_faction, _gce)
			if _activated_card == true: 
				return
	return

func _request_before_card_activation_for_faction(_faction:Enum.Faction, _gce:GameChangeEvent) -> bool:
	DebugUtilities.print_peer(str("Requesting before card to "  + Enum.Faction.keys()[_faction]))
	var _activation_options:Array[CardActivationOption] = DeckState.for_faction(_faction).activatable_cards(true)
	if _activation_options.size() > 0:		
		GameManager.my_input_manager.set_activate_before_action_input_active(_faction, _gce)
		var _activation_option:CardActivationOption = await EventBusLocal.card_selected
		InputMessageLabel.hide_node()
		PlayerActionLabel.hide_node()
		if _activation_option.card_id > -1:
			DeckState.for_faction(_faction).activate_card(_activation_option)
			await _activation_option.change_event.finished
			return true
		else:
			return false
	else:
		DebugUtilities.print_peer(str("Player does not have activatable card"))
		await GameManager.create_timer(200)
	return false

###################
# CARD ACTIVATION #
###################
func request_card_activation(_gce_id:int):
	var _gce:GameChangeEvent = GameChangeEvent.for_id(_gce_id)
	last_activating_team = StaticGameData.faction_team_for_faction(_gce.triggering_faction)
	if _gce.is_trigger:
		for _faction in request_order:            
			var _activated_card:bool = await _request_card_activation_for_faction(_faction)
			if _activated_card == true: 
				return
		action_finished.emit()

func _request_card_activation_for_faction( _faction:Enum.Faction) -> bool:
	DebugUtilities.print_peer(str("Requesting status card to"  + Enum.Faction.keys()[_faction]))
	var _activation_options:Array[CardActivationOption] = DeckState.for_faction(_faction).activatable_cards()
	if _activation_options.size() > 0:		
		GameManager.my_input_manager.set_activate_action_input_active(_faction)
		var _activation_option:CardActivationOption = await EventBusLocal.card_selected
		InputMessageLabel.hide_node()
		PlayerActionLabel.hide_node()
		if _activation_option.card_id > -1:
			DeckState.for_faction(_faction).activate_card(_activation_option)
			return true
		else:
			return false
	else:
		DebugUtilities.print_peer(str("Player does not have activatable card"))
		await GameManager.create_timer(200)
	return false
