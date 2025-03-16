class_name FactionInfoRow extends Control


var faction:Enum.Faction
var faction_state:FactionState:
	get: return GameManager.game_state.faction_state_for_enum(faction)
var background_panel:Panel:
	get: return %BackgroundPanel
var score_label:Label:
	get: return %ScoreLabel

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	var _faction_color:Color = faction_state.faction_data.color()	
	background_panel.self_modulate = Color.WHITE	
	var _style_box:StyleBoxFlat = background_panel.get_theme_stylebox("panel").duplicate()
	_style_box.bg_color = _faction_color
	background_panel.add_theme_stylebox_override("panel", _style_box)
	_set_score(faction_state.score)

	EventBusLocal.faction_scored_points.connect(
		func(_faction:Enum.Faction, _new_score:int): 
			if faction == _faction:
				_set_score(_new_score)
			)

func _set_score(_score:int):
	self.score_label.text = str(_score)
	
	

