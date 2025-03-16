class_name IInputHandler

func _init() -> void:
	assert(false)

func on_key_clicked(_key_event:InputEventKey):
	assert(false)
	

func _number_to_key(_key_number:int)->Key:
	if _key_number < 0 || _key_number > 9:
		assert(false,"There is no key lower than 0 or higher than 9")
	var key:Key = OS.find_keycode_from_string(str("KEY_",_key_number))	
	return key
	
