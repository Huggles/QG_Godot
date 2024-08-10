extends MultiplayerSynchronizer


# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	set_process(get_multiplayer_authority() == 1)
