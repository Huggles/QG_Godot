extends Label


# Called when the node enters the scene tree for the first time.
func _ready() -> void:	
	var args = DebugUtilities._parse_cmd_arguments()
	
	if args.has("host") && args["host"] == true:
		self.text = "I am the host"
	else:
		self.text = ""
