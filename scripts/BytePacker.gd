###
# BytePacker.gd
###
extends Object
class_name BytePacker
 
enum TYPE {
	VARIANT,	# Needs Special Treatment
	BOOL,
 
	CHAR,
	UTF32,
 
	COLOR,
 
	HALF,
	FLOAT,
	DOUBLE,
 
	UINT8,
	UINT16,
	UINT32,
	UINT64,
 
	SINT8,
	SINT16,
	SINT32,
	SINT64,
 
	VECTOR2,
	VECTOR2_INT,
	VECTOR3,
	VECTOR3_INT,
 
	TRANSFORM2D,
	TRANSFORM3D,
	BASIS,
	QUATERNION,
 
	# These Types have a variable size
	RESOURCE,		# Max 65535 Bytes
	BIG_RESOURCE,	# Max 4294967295 Bytes
 
	STRING,	# Max 255 Bytes / 60+ Characters
	LONG_STRING, 	# Max 65535 Bytes / 16300+ Characters
 
	COMPRESSED_BYTES,		# Max 65535 Bytes / 65 KB
	BIG_COMPRESSED_BYTES, 	# Max 4294967295 Bytes / 4 GB
}
 
static func pack(type:TYPE, value:Variant) -> PackedByteArray:
	match type:
		TYPE.BOOL:
			return BytePacker.bool_to_bytes(value)
		TYPE.CHAR:
			return BytePacker.char_to_bytes(value)
		TYPE.UTF32:
			return BytePacker.utf32_to_bytes(value)
		TYPE.COLOR:
			return BytePacker.color_to_bytes(value)
		TYPE.HALF:
			return BytePacker.half_to_bytes(value)
		TYPE.FLOAT:
			return BytePacker.float_to_bytes(value)
		TYPE.DOUBLE:
			return BytePacker.double_to_bytes(value)
		TYPE.UINT8:
			return BytePacker.uint8_to_bytes(value)
		TYPE.UINT16:
			return BytePacker.uint16_to_bytes(value)
		TYPE.UINT32:
			return BytePacker.uint32_to_bytes(value)
		TYPE.UINT64:
			return BytePacker.uint64_to_bytes(value)
		TYPE.SINT8:
			return BytePacker.sint8_to_bytes(value)
		TYPE.SINT16:
			return BytePacker.sint16_to_bytes(value)
		TYPE.SINT32:
			return BytePacker.sint32_to_bytes(value)
		TYPE.SINT64:
			return BytePacker.sint64_to_bytes(value)
		TYPE.VECTOR2:
			return BytePacker.vector2_to_bytes(value)
		TYPE.VECTOR2_INT:
			return BytePacker.vector2i_to_bytes(value)
		TYPE.VECTOR3:
			return BytePacker.vector3_to_bytes(value)
		TYPE.VECTOR3_INT:
			return BytePacker.vector3i_to_bytes(value)
		TYPE.TRANSFORM2D:
			return BytePacker.transform2d_to_bytes(value)
		TYPE.TRANSFORM3D:
			return BytePacker.transform3d_to_bytes(value)
		TYPE.BASIS:
			return BytePacker.basis_to_bytes(value)
		TYPE.QUATERNION:
			return BytePacker.quat_to_bytes(value)
		TYPE.RESOURCE:
			return BytePacker.resource_to_bytes(value, false, true)
		TYPE.BIG_RESOURCE:
			return BytePacker.resource_to_bytes(value, true, true)
		TYPE.STRING:
			return BytePacker.string_to_bytes(value, false)
		TYPE.LONG_STRING:
			return BytePacker.string_to_bytes(value, true)
		TYPE.COMPRESSED_BYTES:
			return BytePacker.compress_bytes(value)
		TYPE.BIG_COMPRESSED_BYTES:
			return BytePacker.compress_bytes(value, true)
		_:
			return var_to_bytes(value)
 
static func bool_to_bytes(value:bool) -> PackedByteArray:
	if value:
		return PackedByteArray([1])
	return PackedByteArray([0])
 
static func char_to_bytes(value:String) -> PackedByteArray:
	return PackedByteArray([value.to_ascii_buffer()[0]])
 
static func utf32_to_bytes(value:String) -> PackedByteArray:
	return value.substr(0, 1).to_utf32_buffer()
 
static func color_to_bytes(value:Color) -> PackedByteArray:
	return BytePacker.sint64_to_bytes(value.to_rgba32())
 
static func half_to_bytes(value:float) -> PackedByteArray:
	var bytes := PackedByteArray([0, 0])
	bytes.encode_half(0, value)
	return bytes
 
static func float_to_bytes(value:float) -> PackedByteArray:
	var bytes := PackedByteArray([0, 0, 0, 0])
	bytes.encode_float(0, value)
	return bytes
 
static func double_to_bytes(value:float) -> PackedByteArray:
	var bytes := PackedByteArray([0, 0, 0, 0, 0, 0, 0, 0])
	bytes.encode_float(0, value)
	return bytes
 
static func uint8_to_bytes(value:int) -> PackedByteArray:
	var bytes := PackedByteArray([0])
	bytes.encode_u8(0, value)
	return bytes
 
static func uint16_to_bytes(value:int) -> PackedByteArray:
	var bytes := PackedByteArray([0, 0])
	bytes.encode_u16(0, value)
	return bytes
 
static func uint32_to_bytes(value:int) -> PackedByteArray:
	var bytes := PackedByteArray([0, 0, 0, 0])
	bytes.encode_u32(0, value)
	return bytes
 
static func uint64_to_bytes(value:int) -> PackedByteArray:
	var bytes := PackedByteArray([0, 0, 0, 0, 0, 0, 0, 0])
	bytes.encode_u64(0, value)
	return bytes
 
static func sint8_to_bytes(value:int) -> PackedByteArray:
	var bytes := PackedByteArray([0])
	bytes.encode_s8(0, value)
	return bytes
 
static func sint16_to_bytes(value:int) -> PackedByteArray:
	var bytes := PackedByteArray([0, 0])
	bytes.encode_s16(0, value)
	return bytes
 
static func sint32_to_bytes(value:int) -> PackedByteArray:
	var bytes := PackedByteArray([0, 0, 0, 0])
	bytes.encode_s32(0, value)
	return bytes
 
static func sint64_to_bytes(value:int) -> PackedByteArray:
	var bytes := PackedByteArray([0, 0, 0, 0, 0, 0, 0, 0])
	bytes.encode_s64(0, value)
	return bytes
 
static func vector2_to_bytes(value:Vector2) -> PackedByteArray:
	var bytes := PackedByteArray([0, 0, 0, 0, 0, 0, 0, 0])
	bytes.encode_float(0, value.x)
	bytes.encode_float(4, value.y)
	return bytes
 
static func vector3_to_bytes(value:Vector3) -> PackedByteArray:
	var bytes := PackedByteArray([0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0])
	bytes.encode_float(0, value.x)
	bytes.encode_float(4, value.y)
	bytes.encode_float(8, value.z)
	return bytes
 
static func vector2i_to_bytes(value:Vector2i) -> PackedByteArray:
	var bytes := PackedByteArray([0, 0, 0, 0, 0, 0, 0, 0])
	bytes.encode_s32(0, value.x)
	bytes.encode_s32(4, value.y)
	return bytes
 
static func vector3i_to_bytes(value:Vector3i) -> PackedByteArray:
	var bytes := PackedByteArray([0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0])
	bytes.encode_s32(0, value.x)
	bytes.encode_s32(4, value.y)
	bytes.encode_s32(8, value.z)
	return bytes
 
static func transform2d_to_bytes(value:Transform2D) -> PackedByteArray:
	return BytePacker.vector2_to_bytes(value.x) + BytePacker.vector2_to_bytes(value.y) + BytePacker.vector2_to_bytes(value.origin)
 
static func transform3d_to_bytes(value:Transform3D) -> PackedByteArray:
	return BytePacker.vector3_to_bytes(value.origin) + BytePacker.basis_to_bytes(value.basis)
 
static func basis_to_bytes(value:Basis) -> PackedByteArray:
	return BytePacker.vector3_to_bytes(value.x) + BytePacker.vector3_to_bytes(value.y) + BytePacker.vector3_to_bytes(value.z)
 
static func quat_to_bytes(value:Quaternion) -> PackedByteArray:
	var bytes := PackedByteArray([0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0])
	bytes.encode_float(0, value.x)
	bytes.encode_float(4, value.y)
	bytes.encode_float(8, value.z)
	bytes.encode_float(12, value.w)
	return bytes
 
static func resource_to_bytes(value:SharedResource, is_large_file:bool = false, full:bool = false) -> PackedByteArray:
	var bytes := value.get_bytes(full)
	var size_byte := PackedByteArray()
	if is_large_file:
		size_byte.resize(4)
		size_byte.encode_u32(0, len(bytes))
	else:
		size_byte.resize(2)
		size_byte.encode_u16(0, len(bytes))
	return size_byte + bytes
 
static func string_to_bytes(value:String, is_large_file:bool = false) -> PackedByteArray:
	var bytes := value.to_utf8_buffer()
	var size_byte := PackedByteArray()
	if is_large_file:
		size_byte.resize(4)
		size_byte.encode_u32(0, len(bytes))
	else:
		size_byte.resize(2)
		size_byte.encode_u16(0, len(bytes))
	return size_byte + bytes
 
static func compress_bytes(value:PackedByteArray, is_large_file:bool = false) -> PackedByteArray:
	var _bytes := value.compress(FileAccess.COMPRESSION_FASTLZ)
	if is_large_file:
		var _size := PackedByteArray([0, 0, 0, 0])
		_size.encode_u32(0, value.size())
		return _size + _bytes
	else:
		var _size := PackedByteArray([0, 0])
		_size.encode_u16(0, value.size())
		return _size + _bytes
 
static func unpack(type:TYPE, bytes:PackedByteArray) -> Variant:
	match type:
		TYPE.BOOL:
			return BytePacker.bytes_to_bool(bytes)
		TYPE.CHAR:
			return BytePacker.bytes_to_char(bytes)
		TYPE.UTF32:
			return BytePacker.bytes_to_utf32(bytes)
		TYPE.COLOR:
			return BytePacker.bytes_to_color(bytes)
		TYPE.HALF:
			return BytePacker.bytes_to_half(bytes)
		TYPE.FLOAT:
			return BytePacker.bytes_to_float(bytes)
		TYPE.DOUBLE:
			return BytePacker.bytes_to_double(bytes)
		TYPE.UINT8:
			return BytePacker.bytes_to_uint8(bytes)
		TYPE.UINT16:
			return BytePacker.bytes_to_uint16(bytes)
		TYPE.UINT32:
			return BytePacker.bytes_to_uint32(bytes)
		TYPE.UINT64:
			return BytePacker.bytes_to_uint64(bytes)
		TYPE.SINT8:
			return BytePacker.bytes_to_sint8(bytes)
		TYPE.SINT16:
			return BytePacker.bytes_to_sint16(bytes)
		TYPE.SINT32:
			return BytePacker.bytes_to_sint32(bytes)
		TYPE.SINT64:
			return BytePacker.bytes_to_sint64(bytes)
		TYPE.VECTOR2:
			return BytePacker.bytes_to_vector2(bytes)
		TYPE.VECTOR2_INT:
			return BytePacker.bytes_to_vector2i(bytes)
		TYPE.VECTOR3:
			return BytePacker.bytes_to_vector3(bytes)
		TYPE.VECTOR3_INT:
			return BytePacker.bytes_to_vector3i(bytes)
		TYPE.TRANSFORM2D:
			return BytePacker.bytes_to_transform2d(bytes)
		TYPE.TRANSFORM3D:
			return BytePacker.bytes_to_transform3d(bytes)
		TYPE.BASIS:
			return BytePacker.bytes_to_basis(bytes)
		TYPE.QUATERNION:
			return BytePacker.bytes_to_quat(bytes)
		TYPE.COMPRESSED_BYTES:
			return BytePacker.decompress_bytes(bytes)
		TYPE.BIG_COMPRESSED_BYTES:
			return BytePacker.decompress_bytes(bytes, true)
		_:
			return bytes_to_var(bytes)
 
static func bytes_to_bool(bytes:PackedByteArray) -> bool:
	return bytes[0] >= 1
 
static func bytes_to_char(bytes:PackedByteArray) -> String:
	return bytes.get_string_from_ascii()
 
static func bytes_to_utf32(bytes:PackedByteArray) -> String:
	return bytes.get_string_from_utf32()
 
static func bytes_to_color(bytes:PackedByteArray) -> Color:
	return Color.hex(bytes.decode_s32(0))
 
static func bytes_to_half(bytes:PackedByteArray) -> float:
	return bytes.decode_half(0)
 
static func bytes_to_float(bytes:PackedByteArray) -> float:
	return bytes.decode_float(0)
 
static func bytes_to_double(bytes:PackedByteArray) -> float:
	return bytes.decode_double(0)
 
static func bytes_to_uint8(bytes:PackedByteArray) -> int:
	return bytes.decode_u8(0)
 
static func bytes_to_uint16(bytes:PackedByteArray) -> int:
	return bytes.decode_u16(0)
 
static func bytes_to_uint32(bytes:PackedByteArray) -> int:
	return bytes.decode_u8(0)
 
static func bytes_to_uint64(bytes:PackedByteArray) -> int:
	return bytes.decode_u64(0)
 
static func bytes_to_sint8(bytes:PackedByteArray) -> int:
	return bytes.decode_s8(0)
 
static func bytes_to_sint16(bytes:PackedByteArray) -> int:
	return bytes.decode_s16(0)
 
static func bytes_to_sint32(bytes:PackedByteArray) -> int:
	return bytes.decode_s8(0)
 
static func bytes_to_sint64(bytes:PackedByteArray) -> int:
	return bytes.decode_s64(0)
 
static func bytes_to_vector2(bytes:PackedByteArray) -> Vector2:
	return Vector2(
		bytes.decode_float(0),
		bytes.decode_float(4))
 
static func bytes_to_vector3(bytes:PackedByteArray) -> Vector3:
	return Vector3(
		bytes.decode_float(0),
		bytes.decode_float(4),
		bytes.decode_float(8))
 
static func bytes_to_vector2i(bytes:PackedByteArray) -> Vector2i:
	return Vector2(
		bytes.decode_s32(0),
		bytes.decode_s32(4))
 
static func bytes_to_vector3i(bytes:PackedByteArray) -> Vector3i:
	return Vector3(
		bytes.decode_s32(0),
		bytes.decode_s32(4),
		bytes.decode_s32(8))
 
static func bytes_to_transform2d(bytes:PackedByteArray) -> Transform2D:
	return Transform2D(BytePacker.bytes_to_vector2(bytes.slice(0, 8)), BytePacker.bytes_to_vector2(bytes.slice(8, 16)), BytePacker.bytes_to_vector2(bytes.slice(16, 24)))
 
static func bytes_to_transform3d(bytes:PackedByteArray) -> Transform3D:
	return Transform3D(BytePacker.bytes_to_basis(bytes.slice(12, 48)), BytePacker.bytes_to_vector3(bytes.slice(0, 12)))
 
static func bytes_to_basis(bytes:PackedByteArray) -> Basis:
	return Basis(
		BytePacker.bytes_to_vector3(bytes.slice(0, 12)),
		BytePacker.bytes_to_vector3(bytes.slice(12, 24)),
		BytePacker.bytes_to_vector3(bytes.slice(24, 36)))
 
static func bytes_to_quat(bytes:PackedByteArray) -> Quaternion:
	return Quaternion(bytes.decode_float(0), bytes.decode_float(4), bytes.decode_float(8), bytes.decode_float(12))
 
static func bytes_to_resource(bytes:PackedByteArray, value:SharedResource, is_large_file:bool = false) -> SharedResource:
	if is_large_file:
		var size := bytes.decode_u32(0)
		value.read_bytes(bytes.slice(4, size + 4))
	else:
		var size := bytes.decode_u16(0)
		value.read_bytes(bytes.slice(2, size + 2))
	return value
 
static func bytes_to_string(bytes:PackedByteArray, is_large_file:bool = false) -> String:
	if is_large_file:
		var size := bytes.decode_u32(0)
		return bytes.slice(4, size + 4).get_string_from_utf8()
	var size := bytes.decode_u16(0)
	return bytes.slice(2, size + 2).get_string_from_utf8()
 
static func decompress_bytes(bytes:PackedByteArray, is_large_file:bool = false) -> PackedByteArray:
	if is_large_file:
		var _size := bytes.decode_u32(0)
		return bytes.slice(4).decompress(_size, FileAccess.COMPRESSION_FASTLZ)
	var _size := bytes.decode_u16(0)
	return bytes.slice(2).decompress(_size, FileAccess.COMPRESSION_FASTLZ)
 
static func get_byte_size(type:TYPE) -> int:
	match type:
		TYPE.BOOL:
			return 1
		TYPE.CHAR:
			return 1
		TYPE.UTF32:
			return 4
		TYPE.COLOR:
			return 4
		TYPE.HALF:
			return 2
		TYPE.FLOAT:
			return 4
		TYPE.DOUBLE:
			return 8
		TYPE.UINT8:
			return 1
		TYPE.UINT16:
			return 2
		TYPE.UINT32:
			return 4
		TYPE.UINT64:
			return 8
		TYPE.SINT8:
			return 1
		TYPE.SINT16:
			return 2
		TYPE.SINT32:
			return 4
		TYPE.SINT64:
			return 8
		TYPE.VECTOR2:
			return 8
		TYPE.VECTOR2_INT:
			return 8
		TYPE.VECTOR3:
			return 12
		TYPE.VECTOR3_INT:
			return 12
		TYPE.TRANSFORM2D:
			return 24
		TYPE.TRANSFORM3D:
			return 48
		TYPE.BASIS:
			return 36
		TYPE.QUATERNION:
			return 16
		_:
			return 1
 
static func packed_color_array_to_bytes(colors:PackedColorArray) -> PackedByteArray:
	var ints := PackedInt32Array()
	ints.resize(colors.size())
	for i in range(ints.size()):
		ints[i] = colors[i].to_rgba32()
	return ints.to_byte_array()
 
static func bytes_to_packed_color_array(bytes:PackedByteArray) -> PackedColorArray:
	var ints := bytes.to_int32_array()
	var colors := PackedColorArray()
	colors.resize(ints.size())
	for i in range(ints.size()):
		colors[i] = Color.hex(ints[i])
	return colors
