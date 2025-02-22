###
# SharedProperty.gd
###
@tool
extends Resource
class_name SharedProperty
 
static func get_bytes(obj:Object, shared_property_list:Array[SharedProperty], full:bool = false, _special:Dictionary = {}) -> PackedByteArray:
	var bytes := StreamPeerBuffer.new()
	for prop in shared_property_list:
		if not full and not prop.keep_in_sync:
			continue
		var value = obj.get(prop.property)
		assert(value != null, "UNKNOWN SHARED PROPERTY {prop}".format({ "prop": prop.property }))
		if prop.array_mode != ARRAY_MODE.NONE:
			match prop.array_mode:
				ARRAY_MODE.SMALL:
					bytes.put_u8(len(value))
				ARRAY_MODE.BIG:
					bytes.put_u16(len(value))
				ARRAY_MODE.MAX:
					bytes.put_u32(len(value))
		match prop.type:
			BytePacker.TYPE.RESOURCE:
				if prop.array_mode != ARRAY_MODE.NONE:
					for sub in value:
						bytes.put_data(BytePacker.resource_to_bytes(sub, false, full))
				else:
					bytes.put_data(BytePacker.resource_to_bytes(value, false, full))
			BytePacker.TYPE.BIG_RESOURCE:
				if prop.array_mode != ARRAY_MODE.NONE:
					for sub in value:
						bytes.put_data(BytePacker.resource_to_bytes(sub, true, full))
				else:
					bytes.put_data(BytePacker.resource_to_bytes(value, true, full))
			_:
				if prop.array_mode != ARRAY_MODE.NONE:
					for sub in value:
						bytes.put_data(BytePacker.pack(prop.type, sub))
				else:
					bytes.put_data(BytePacker.pack(prop.type, value))
	return bytes.data_array
 
static func read_bytes(obj:Object, bytes:PackedByteArray, shared_property_list:Array[SharedProperty], full:bool = false, _special:Dictionary = {}):
	var stream := StreamPeerBuffer.new()
	stream.data_array = bytes
	for prop in shared_property_list:
		if not full and not prop.keep_in_sync:
			continue
		if prop.array_mode != SharedProperty.ARRAY_MODE.NONE:
			var array_length:int = 0
			match prop.array_mode:
				SharedProperty.ARRAY_MODE.SMALL:
					array_length = stream.get_u8()
				SharedProperty.ARRAY_MODE.BIG:
					array_length = stream.get_u16()
				SharedProperty.ARRAY_MODE.MAX:
					array_length = stream.get_u32()
			var arr := []
			arr.resize(array_length)
			for i in range(array_length):
				arr[i] = prop.read_value_from_stream(stream)
			obj.set(prop.property, arr)
		else:
			if prop.special == SharedProperty.SPECIAL.TWEEN and obj is Node:
				obj.create_tween().tween_property(obj, prop.property, prop.read_value_from_stream(stream), _special.get(SPECIAL.TWEEN, 0.05))
			else:
				if prop.type == BytePacker.TYPE.RESOURCE || prop.type == BytePacker.TYPE.BIG_RESOURCE:
					obj.get(prop.property).read_bytes(prop.read_value_from_stream(stream))
				else:
					obj.set(prop.property, prop.read_value_from_stream(stream))
 
enum ARRAY_MODE {
	NONE,		# Single Value
	SMALL,		# Max Size - 255
	BIG,		# Max Size - 65535
	MAX,		# Max Size - 4294967295
}
 
enum SPECIAL {
	NONE,
	TWEEN,
}
 
## Property Name
@export var property:String = "" :
	set(value):
		property = value
		self.resource_name = value
## Type for the BytePacker [br]
## Resource must be SharedResource [br]
## LongResource can be uint32 bytes long; Resource only uint16
@export var type:BytePacker.TYPE = BytePacker.TYPE.VARIANT
@export var array_mode:ARRAY_MODE = ARRAY_MODE.NONE
@export var special:SPECIAL = SPECIAL.NONE
## If not kept in sync, property will only be shared ONCE at creation.
@export var keep_in_sync:bool = false
 
func read_value_from_stream(stream:StreamPeerBuffer) -> Variant:
	match type:
		BytePacker.TYPE.RESOURCE:
			var size := stream.get_u16()
			return stream.get_data(size)[1]
		BytePacker.TYPE.BIG_RESOURCE:
			var size := stream.get_u32()
			return stream.get_data(size)[1]
		BytePacker.TYPE.STRING:
			var size := stream.get_u8()
			return (stream.get_data(size)[1]).get_string_from_utf8()
		BytePacker.TYPE.LONG_STRING:
			var size := stream.get_u16()
			return (stream.get_data(size)[1]).get_string_from_utf8()
		_:
			return  BytePacker.unpack(type, stream.get_data(BytePacker.get_byte_size(type))[1])
 
func with_type(value:BytePacker.TYPE) -> SharedProperty:
	self.type = value
	return self
 
func as_array(mode:ARRAY_MODE = ARRAY_MODE.SMALL) -> SharedProperty:
	self.array_mode = mode
	return self
 
func with_special(value:SPECIAL) -> SharedProperty:
	self.special = value
	return self
 
func in_sync(value:bool = true) -> SharedProperty:
	self.keep_in_sync = value
	return self
 
func _init(prop:String = ""):
	self.property = prop
 
