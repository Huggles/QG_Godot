extends Node
class_name BattleUnitChangeEvent

var unit_id:int
var country_id:int
var attack_type:Enum.AttackType

func _init(_unit_id:int, _country_id:int, _attack_type:Enum.AttackType) -> void:
	self.event_type 			= "ATTACK_UNIT"
	self.unit_id 			= _unit_id
	self.country_id 			= _country_id
	self.attack_type 		= _attack_type
