class_name StraightState extends StateObject


var straight_icon = preload("res://assets/textures/Other/StraightIcon.png")
var straight_icon_inverse = preload("res://assets/textures/Other/StraightIconInverse.png")

var static_straight_data:StraightData

var id:int
var controlling_country_id:int
var controlling_country_state:CountryState:	
	get: return CountryState.for_id(controlling_country_id)

var controlled_country_id_1:int
var controlled_country_state_1:CountryState:
	get: return CountryState.for_id(controlled_country_id_1)
	
var controlled_country_id_2:int
var controlled_country_state_2:CountryState:
	get: return CountryState.for_id(controlled_country_id_2)
	
var straight_sprite_node:Sprite3D:
	get: return controlling_country_state.node.straight_sprite_node

func _init(_controlling_country_id:int, _static_straight_data:StraightData) -> void:
	self.static_straight_data = _static_straight_data
	self.controlling_country_id =_controlling_country_id
	if _static_straight_data.is_straight:
		self.controlled_country_id_1 = _static_straight_data.controlled_country_1_id
		self.controlled_country_id_2 = _static_straight_data.controlled_country_2_id
	
func on_ready():	
	if static_straight_data.is_straight:
		show_straight_sprite()		
	else:
		hide_straight_sprite()
	recalulate_controlled_by()
		
func recalulate_controlled_by():
	change_color_team(controlling_country_state.occupying_team)

func controlled_by_faction() -> Enum.FactionTeam:
	return controlling_country_state.occupying_team
	
func show_straight_sprite():
	if static_straight_data.icon_inversed:
		straight_sprite_node.texture = straight_icon_inverse
	else:
		straight_sprite_node.texture = straight_icon
	straight_sprite_node.visible = true
	straight_sprite_node.position = Vector3(static_straight_data.straight_transform.x_position,static_straight_data.straight_transform.y_position,static_straight_data.straight_transform.z_position)
	straight_sprite_node.rotation_degrees = Vector3(static_straight_data.straight_transform.x_rotation,static_straight_data.straight_transform.y_rotation,static_straight_data.straight_transform.z_rotation)
	straight_sprite_node.scale = Vector3(static_straight_data.straight_transform.scale,static_straight_data.straight_transform.scale,static_straight_data.straight_transform.scale)

func hide_straight_sprite():
	straight_sprite_node.visible = false
	
func change_color_team(_controlling_team:Enum.FactionTeam):
	if _controlling_team == Enum.FactionTeam.AXIS:
		straight_sprite_node.modulate = Color.RED
	if _controlling_team == Enum.FactionTeam.ALLIES:
		straight_sprite_node.modulate = Color.DARK_BLUE
	if _controlling_team == Enum.FactionTeam.NONE:
		straight_sprite_node.modulate = Color.BLUE
	pass
