class_name InputMessageLabel extends RichTextLabel


static var current_text:String
static var instance:InputMessageLabel

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	self.bbcode_enabled = true
	if InputMessageLabel.instance == null:
		InputMessageLabel.instance = self
		if current_text != null:
			self.text = current_text
			pass
		else: 
			self.hide_node()
			pass
			
	else: 
		assert(false, "Can only have 1 input message label")

static func show_text(_text:String):
	current_text = _text	
	if instance != null:		
		instance.text = current_text
		instance.visible = true
		if instance.get_parent() != null:
			instance.get_parent().visible = true		



static func hide_node():
	if instance != null:
		instance.current_text = String()
		instance.visible = false
		if instance.get_parent() != null:
			instance.get_parent().visible = false
		

