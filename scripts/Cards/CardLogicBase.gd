class_name CardLogicBase extends Object

var peer_id:int
var card_state:CardState
var card_data:CardData:
	get: return card_state.card_data
var faction:Enum.Faction:
	get: return card_state.faction
var faction_data:FactionData:
	get: return StaticGameData.faction_data_map[faction] if StaticGameData.faction_data_map.has(faction) else null;

var player:PlayerScene:
	get: return StaticGameData.faction_player_map[faction_data.name] if faction_data != null else null;		

var is_played:bool
var activated_in_turns:Array[int]
var is_activated_once:bool:
	get: return activated_in_turns.size() > 0
var is_activated_this_turn:bool:
	get: return activated_in_turns.has(GameManager.game_flow.game_turn)

var is_publicly_visible:bool:
	get: return is_played || (card_data.type == "RESPONSE" && is_activated_once)

var completed_parts:Array[int]

# var card_front_texture: Texture2D: 
# 	get:
# 		match card_data.type:
# 			"BUILD_ARMY": return load(faction_data.card_front_build_army_texture)	
# 			"BUILD_NAVY": return load(faction_data.card_front_build_navy_texture)	
# 			"LAND_BATTLE": return load(faction_data.card_front_land_battle_texture)	
# 			"SEA_BATTLE": return load(faction_data.card_front_sea_battle_texture)	
# 			"STATUS": return load(faction_data.card_front_status_texture)	
# 			"RESPONSE": return load(faction_data.card_front_response_texture)	
# 			"EVENT": return load(faction_data.card_front_event_texture)	
# 			"EW": return load(faction_data.card_front_ew_texture)	
# 		return 
	
# var card_back_texture: Texture2D:
# 	get: return load(faction_data.card_back_texture)
	
func _init(_card_state:CardState)->void:	
	card_state = _card_state	

func can_play_card() -> bool:
	return _can_play_card(part_counter)

func _can_play_card(_part:int) -> bool:
	return true
	
func can_activate_action(_game_change_event:GameChangeEvent) -> bool:
	var _activatable:bool = _can_activate_action(_game_change_event) && !is_activated_this_turn
	DebugUtilities.print_peer(str(self.card_data.clabel, " is activatable: ", _activatable, " for ", _game_change_event.summary_text()))
	return _activatable

func _can_activate_action(_game_change_event:GameChangeEvent) -> bool:
	return false




func play_card():
	if _can_play_card(part_counter):
		PlayerActionLabel.show_text(_play_action_guidance(part_counter))		
		_play_card(part_counter)
	else:
		DebugUtilities.print_peer_err(str("Cannot execute card: ", card_data.name))

func activate_card(_game_change_event:GameChangeEvent):
	if can_activate_action(_game_change_event):
		EventBusLocal.status_card_activation_started.emit(self.card_state.id)
		activated_in_turns.push_back(GameManager.game_flow.game_turn)
		PlayerActionLabel.show_text(_activate_action_guidance(part_counter))		
		_activate_action(_game_change_event, part_counter)			
	else:
		DebugUtilities.print_peer_err(str("Cannot activate card: ", card_data.name))

func _play_action_guidance(_part:int)->String:
	return str("Play ", self.get_script().get_global_name())

func _activate_action_guidance(_part:int)->String:
	return str("Activate ", self.get_script().get_global_name())

func _play_card(_part:int):
	pass

func _activate_action(_game_change_event:GameChangeEvent, part:int):
	pass

#For cards with multiple actions
var part_counter = 1
func _has_multiple_actions() -> bool:
	return false

func has_next_action() -> bool:
	return _has_next_action()

func _has_next_action() -> bool:
	return false

func play_next_action() -> void:
	part_counter += 1
	play_card()

func activate_next_action(_game_change_event:GameChangeEvent) -> void:
	part_counter += 1
	activate_card(_game_change_event)


func card_play_finished():
	print(str("Finished play: ",self.get_script().get_global_name()))
	EventBusLocal.card_play_completed.emit(self.card_state.id)
	pass

func card_activation_finished():	
	print(str("Finished activation: ",self.get_script().get_global_name()))
	completed_parts = []
	EventBusLocal.status_card_activation_completed.emit(self.card_state.id)
	pass
	

	
