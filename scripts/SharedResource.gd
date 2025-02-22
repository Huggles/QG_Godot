
###
# SharedResource.gd
###
extends Resource
class_name SharedResource
 
func get_bytes(full:bool = false) -> PackedByteArray:
	return SharedProperty.get_bytes(self, get_shared_properties(), full)
 
func read_bytes(bytes:PackedByteArray, full:bool = false):
	SharedProperty.read_bytes(self, bytes, get_shared_properties(), full)
 
func get_shared_properties() -> Array[SharedProperty]:
	return []
 
func load_from_file(file_path:String) -> int:
	if not FileAccess.file_exists(file_path):
		return OK
	var bytes := FileAccess.get_file_as_bytes(file_path)
	self.read_bytes(bytes, true)
	return OK
 
func save_to_file(file_path:String) -> int:
	var file := FileAccess.open(file_path, FileAccess.WRITE)
	if file == null:
		return FileAccess.get_open_error()
	file.store_buffer(self.get_bytes(true))
	file.close()
	return OK
 
