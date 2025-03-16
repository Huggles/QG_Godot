class_name StraightData extends DataObject

var is_straight:bool
var icon_inversed:bool
var controlled_country_1:String
var controlled_country_2:String

var controlled_country_1_id:int:	
	get: 
		var _country_state = CountryState.for_name(controlled_country_1)
		return _country_state.id if _country_state != null else int()
var controlled_country_2_id:int:
	get: 
		var _country_state = CountryState.for_name(controlled_country_2)
		return _country_state.id if _country_state != null else int()

var straight_transform:TransformData

func _init(json):
	super(json)
	if is_straight == false:		
		controlled_country_1 = String()
		controlled_country_2 = String()
