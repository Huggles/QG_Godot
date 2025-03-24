class_name FactionInfoRow extends Control


var faction:Enum.Faction
var faction_state:FactionState:
	get: return GameManager.game_state.faction_state_for_enum(faction)
var background_panel:Panel:
	get: return %BackgroundPanel
var score_label:Label:
	get: return %ScoreLabel
var detail_panel:Label:
	get: return %DetailPanel
var turn_summaries_rich_text:RichTextLabel:
	get: return %TurnSummariesRichText



var hover_timer:Timer

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	var _faction_color:Color = faction_state.faction_data.color()	
	background_panel.self_modulate = Color.WHITE	
	var _style_box:StyleBoxFlat = background_panel.get_theme_stylebox("panel").duplicate()
	_style_box.bg_color = _faction_color
	background_panel.add_theme_stylebox_override("panel", _style_box)
	self.score_label.label_settings = self.score_label.label_settings.duplicate()


	_set_score(faction_state.score)	

	EventBusLocal.faction_scored_points.connect(
		func(_faction:Enum.Faction, _new_score:int): 
			print(_faction)
			print(self.faction)
			if self.faction == _faction:
				print(self.faction)
				_set_score(_new_score)
				_load_vp_details()
			)

	EventBusLocal.vp_details_panel_opened.connect(
		func(_faction):
			if _faction != self.faction:
				detail_panel.visible = false
			)

func _set_score(_score:int):
	self.score_label.text = str(_score)
	var tween = get_tree().create_tween()
	tween.tween_property(%ScoreLabel.label_settings, "font_size", 72, 0.2)
	tween.tween_property(%ScoreLabel.label_settings, "font_size", 36, 0.2)
	



var is_mouse_over = false
func _on_mouse_entered() -> void:
	is_mouse_over = true
	pass # Replace with function body.

func _on_mouse_exited() -> void:	
	is_mouse_over = false
	pass # Replace with function body.

func _on_gui_input(event:InputEvent) -> void:	
	if is_mouse_over && event is InputEventMouseButton:
		var _mouse_button_event = event as InputEventMouseButton
		if _mouse_button_event.is_released():
			_toggle_details()

	pass # Replace with function body.

func _toggle_details():
	detail_panel.visible = !detail_panel.visible	
	if detail_panel.visible == true:
		EventBusLocal.vp_details_panel_opened.emit(faction)
		_load_vp_details()

func _load_vp_details():	
	turn_summaries_rich_text.text = String()
	var _text_rows:Array[String] = []
	var _vp_summaries:Array = GameManager.game_flow.victory_point_summaries[faction]
	for _vp_summary:VPTurnSummary in _vp_summaries:
		_text_rows.push_back(str("Turn: ", _vp_summary.turn_number))
		for _reason:String in _vp_summary.scores_for_reason.keys():
			var _points:int = _vp_summary.scores_for_reason[_reason]
			_text_rows.push_back(str(_points, " points for ", _reason))
		
	turn_summaries_rich_text.text = "\n".join(_text_rows)


		

