extends Control

const CARD_SCENE = preload("res://scenes/cards/CardScene.tscn")

@onready var cards_container = $CardsContainerPanel
var faction:String


func _ready() -> void:
	_init_hand()
	
func show_node(_visible:bool):
	cards_container.visible = _visible

func _init_hand():	
	_init_cards( Globals.deck_data_map[faction].deck_cards )	

func _init_cards(cards:Array[CardBase]) -> void:
	_delete_current_cards()
	
	const card_step_size = 100
	const rotation_step_size = 10	
	const card_scale = Vector2(0.5,0.5)
	
	var total_rotation_size = ((cards.size()-1) * rotation_step_size)
	var total_size_x = (cards.size()-1) * card_step_size
	
	
	var index:int = 0
	for card in cards:
		var card_scene_instance:CardScene = CARD_SCENE.instantiate()
		card_scene_instance.card = card
		card_scene_instance.scale = Vector2(0.5,0.5)
		cards_container.add_child(card_scene_instance)		
		card_scene_instance.position = Vector2(cards_container.size.x * card_scale.x,0)
		card_scene_instance.position -= Vector2(card_scene_instance.pivot_offset.x, card_scene_instance.pivot_offset.y/2)
		card_scene_instance.position -= Vector2(total_size_x/2.0, 0)
		card_scene_instance.position += Vector2(index * card_step_size, 0)
		card_scene_instance.z_index = index
		card_scene_instance.set_rotation_degrees(index* rotation_step_size - (total_rotation_size/2.0) )

		
		index+=1

func _delete_current_cards() -> void:
	for card_display in cards_container.get_children():
		cards_container.remove_child(card_display)		
