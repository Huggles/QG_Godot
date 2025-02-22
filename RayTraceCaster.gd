extends Object
class_name RayTraceCaster

const RAY_LENGTH = 5000

var casting_camera:PlayerCamera3D
var current_raycast_collisions = []		
var current_raycast_colliders = []

@export_flags_3d_physics var _sprite_layers = 0x000F


func _init(camera:Camera3D):
	casting_camera = camera;	
	
func cast_rays(_mouse_event:InputEventMouse) -> void:		
	var results = _shoot_rays()	
	var result_colliders = results.map(func(result): return result.collider)		
	var exited_results = [];	
	var entered_results = [];

	for current_raycast_collision in current_raycast_collisions:
		if !result_colliders.has(current_raycast_collision.collider):			
			exited_results.push_back(current_raycast_collision)
			
	for result in results:
		if !current_raycast_colliders.has(result.collider):
			entered_results.push_back(result)
	
	current_raycast_collisions = [];
	current_raycast_colliders = [];
	for result in results:
		current_raycast_collisions.push_back(result)	
		current_raycast_colliders.push_back(result.collider)	
	
	for entered_result in entered_results:
		var collision_object = entered_result.collider			
		if collision_object is RayTraceHandler:
			var rth:RayTraceHandler = collision_object;					
			rth.on_start_hit(casting_camera, _mouse_event, entered_result.position, entered_result.normal)					
		
	for exited_result in exited_results:
		var collision_object = exited_result.collider			
		if collision_object is RayTraceHandler:
			var rth:RayTraceHandler = collision_object;					
			rth.on_stop_hit(casting_camera, _mouse_event, exited_result.position, exited_result.normal)					
	
	for current_raycast_collision in current_raycast_collisions:
		var collision_object = current_raycast_collision.collider			
		if collision_object is RayTraceHandler:
			var rth:RayTraceHandler = collision_object;					
			rth.on_hitting(casting_camera, _mouse_event, current_raycast_collision.position, current_raycast_collision.normal)					
	
	#_debug_targets(current_raycast_colliders)
			
			
func _shoot_rays() -> Array:
	var space_state = casting_camera.get_world_3d().direct_space_state	
	var from = casting_camera.project_ray_origin(casting_camera.mouse_position)
	var to = from + casting_camera.project_ray_normal(casting_camera.mouse_position) * RAY_LENGTH	
	var colliders_to_ignore = []
	var results = []
	var _sprite_layers = 0x000F
	while true:
		var query:PhysicsRayQueryParameters3D = PhysicsRayQueryParameters3D.create(from, to, _sprite_layers, colliders_to_ignore)		
		query.collide_with_areas = true;		
		var result = space_state.intersect_ray(query)		
		if result.is_empty():
			break;		
		results.push_back(result)
		colliders_to_ignore.push_back(result.collider)
	return results
			
func _debug_targets(target_colliders:Array):
	for target_collider in target_colliders:					
		if target_collider is RayTraceHandler:
			var rth:RayTraceHandler = target_collider;		
			print(str("current_raycast_target: ",rth.clickable_sprite.identifier))
		else:
			print(target_collider)
