class_name VPTurnSummary extends StateObject 

var turn_number:int = -1
var scores_for_reason:Dictionary 
var total_score:int:
    get: return Math.sum_array(scores_for_reason.values())

func _init(_turn_number:int) -> void:
    self.turn_number = _turn_number
    scores_for_reason = {}
    pass


func add_score(_points:int, _reason:String):
    scores_for_reason[_reason] = _points
    pass

