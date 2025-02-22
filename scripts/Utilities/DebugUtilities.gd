extends Node

var _cmd_arguments: Dictionary
var cmd_arguments: Dictionary:
	get: 
		if not _cmd_arguments: 
			_cmd_arguments = _parse_cmd_arguments()
		return _cmd_arguments

func print_peer_err(message):
	var messageString:String = str(message);
		
	if MultiplayerManager.multiplayer.is_server():		
		printerr("("+Time.get_time_string_from_system() +")"+" Server: " + messageString)
	else:
		printerr("("+Time.get_time_string_from_system() +")"+" Client: " + messageString)

func print_peer(message):
	var messageString:String = str(message);
		
	if MultiplayerManager.multiplayer.is_server():		
		print("("+Time.get_time_string_from_system() +")"+" Server: " + messageString)
	else:
		print("("+Time.get_time_string_from_system() +")"+" Client: " + messageString)
		
	
	


func _parse_cmd_arguments() -> Dictionary:
	var arguments = {}
	for argument in OS.get_cmdline_args():
		if argument.contains("--"):
			if argument.contains("="):
				var key_value = argument.split("=")
				arguments[key_value[0].trim_prefix("--")] = key_value[1]
			else:
				# Options without an argument will be present in the dictionary,
				# with the value set to an empty string.
				arguments[argument.trim_prefix("--")] = true
	return arguments
