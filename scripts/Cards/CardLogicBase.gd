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

var card_front_texture: Texture2D: 
	get:
		match card_data.type:
			"BUILD_ARMY": return load(faction_data.card_front_build_army_texture)	
			"BUILD_NAVY": return load(faction_data.card_front_build_navy_texture)	
			"LAND_BATTLE": return load(faction_data.card_front_land_battle_texture)	
			"SEA_BATTLE": return load(faction_data.card_front_sea_battle_texture)	
			"STATUS": return load(faction_data.card_front_status_texture)	
			"RESPONSE": return load(faction_data.card_front_response_texture)	
			"EVENT": return load(faction_data.card_front_event_texture)	
			"EW": return load(faction_data.card_front_ew_texture)	
		return 
	
var card_back_texture: Texture2D:
	get: return load(faction_data.card_back_texture)
	
func _init(_card_state:CardState)->void:	
	card_state = _card_state	

func can_play_card() -> bool:
	return true
	
func can_activate_card(_game_change_event:GameChangeEvent) -> bool:
	return true

func play_card():
	if can_play_card():
		_play_card()
	else:
		DebugUtilities.print_peer_err(str("Cannot execute card: ", card_data.name))

func activate_card(_game_change_event:GameChangeEvent):
	if can_activate_card(_game_change_event):
		EventBusLocal.status_card_activation_started.emit(self.card_state.id)
		_activate_card(_game_change_event)
	else:
		DebugUtilities.print_peer_err(str("Cannot activate card: ", card_data.name))

func _play_card():
	pass

func _activate_card(_game_change_event:GameChangeEvent):
	pass

func card_play_finished():
	print(str("Finished play: ",self.get_script().get_global_name()))
	EventBusLocal.card_play_completed.emit(self.card_state.id)
	pass

func card_activation_finished():	
	print(str("Finished activation: ",self.get_script().get_global_name()))
	EventBusLocal.status_card_activation_completed.emit(self.card_state.id)
	pass
	

	
