class_name InputMessageLabel extends RichTextLabel


static var current_text:String
static var current_faction:Enum.Faction = -1
static var instance:InputMessageLabel

var container_panel:Panel:
	get: return %ContainerPanel

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	self.bbcode_enabled = true
	
	if InputMessageLabel.instance == null:
		InputMessageLabel.instance = self
		if current_text != null:
			show_text(current_text, current_faction)
			pass
		else: 
			self.hide_node()
			pass
			
	else: 
		assert(false, "Can only have 1 input message label")

static func show_text(_text:String, _faction:Enum.Faction = -1):
	current_text = _text	
	current_faction = _faction
	if instance != null:		
		instance.text = current_text
		instance.visible = true
		if instance.container_panel != null:
			instance.container_panel.visible = true

static func hide_node():
	if instance != null:
		instance.current_text = String()
		instance.visible = false
		if instance.container_panel != null:
			instance.container_panel.visible = false
		

