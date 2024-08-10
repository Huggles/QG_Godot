extends CardBase
class_name BuildArmy


func execute_card()->bool:
	var unit = GameManager.game_mode.all_units[0]	
	var country = GameManager.game_mode.country_map["Australia"]
	print(unit)
	print(country)
	country.add_unit(unit)
	return true
