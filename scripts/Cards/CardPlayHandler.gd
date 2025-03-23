class_name CardPlayHandler extends Object


var started_change_events:Array[GameChangeEvent]
var succesful_change_events:Array[GameChangeEvent]
var last_activating_team:Enum.FactionTeam

var request_order:Array:
    get:
        var order = [] 
        order.append_array(StaticGameData.opponent_factions_for_team(last_activating_team))
        order.append_array(StaticGameData.factions_for_team(last_activating_team))         
        return order

func _init() -> void:
    enable()

func enable():
    EventBusLocal.game_change_event_before.connect(handle_change_event)
    EventBusLocal.game_change_event_after.connect(request_card_activation)
    EventBusLocal.card_play_completed.connect(request_card_activation)

func disable():
    EventBusLocal.game_change_event_before.disconnect(handle_change_event)
    EventBusLocal.game_change_event_after.disconnect(request_card_activation)
    EventBusLocal.card_play_completed.disconnect(request_card_activation)

func handle_change_event(_gce_id):
    succesful_change_events.push_back( GameChangeEvent.for_id(_gce_id))
    

func request_card_play():
    GameManager.player_states[0].input_manager.set_play_card_input_active()
    GameManager.game_flow.current_faction_deck_state.debug_hand()    
    var _activation_option:CardActivationOption = await EventBusLocal.card_selected
    var _card_state:CardState = CardState.for_id(_activation_option.card_id)    
    DeckState.for_faction(_card_state.faction).play_card(_card_state.id)

func request_card_activation(_gce_id:int):    
    var _gce:GameChangeEvent = GameChangeEvent.for_id(_gce_id)
    last_activating_team = StaticGameData.faction_team_for_faction(_gce.triggering_faction)
    if _gce.is_trigger:
        for _faction in request_order:
            var _activated_card:bool = await _request_card_activation_for_faction(_faction)
            if _activated_card == true: return
        EventBusLocal.card_play_handler_completed.emit(self)
    
    

func _request_card_activation_for_faction( _faction:Enum.Faction) -> bool:
    DebugUtilities.print_peer(str("Requesting status card to"  + Enum.Faction.keys()[_faction]))
    var _activation_options:Array[CardActivationOption] = DeckState.for_faction(_faction).activatable_cards()
    if _activation_options.size() > 0:		
        GameManager.my_input_manager.set_activate_card_input_active(_faction)
        var _activation_option:CardActivationOption = await EventBusLocal.card_selected
        if _activation_option.card_id > -1:
            GameManager.game_flow.current_faction_deck_state.activate_card(_activation_option)
            return true
        else:
            return false
    else:
        DebugUtilities.print_peer(str("Player does not have activatable card"))
        await GameManager.create_timer(200)
    return false
    

    


func _handle()->bool:
    for _faction:Enum.Faction in self.request_order:
        var _activatable_cards:Array[CardActivationOption] = DeckState.for_faction(_faction).activatable_card_ids()	
        if _activatable_cards.size() > 0:
            for _gce:GameChangeEvent in GameManager.game_state:
                pass
                #await request_card_activation(_faction, _gce)
        else:
            DebugUtilities.print_peer(str("Player does not have playable status card"))
    
    await GameManager.create_timer(200) 
    return true


func try_play_card(_card_id:int):
    var _card_state:CardState = CardState.for_id(_card_id)
    if _card_state.can_play_card():
        GameManager.my_input_manager.set_no_input_active()
        InputMessageLabel.hide_node()
        DeckState.for_faction(_card_state.faction).play_card(_card_id)        
    pass
