extends Control
class_name CardScene

@onready var text_container_node:AspectRatioContainer = $Panel/TextContainerNode
@onready var title_node:Label = $Panel/TextContainerNode/TextBoxTexture/VBoxContainer/Title
@onready var text_node:RichTextLabel = $Panel/TextContainerNode/TextBoxTexture/VBoxContainer/Text
@onready var card_texture_node:TextureRect = $Panel/CardTexture

var card:CardLogicBase

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	_build_card()
			
func _build_card()->void:
	if card:	
		self.name = str(card.faction) + "-" + card.card_data.name		
		if card.is_publicly_visible || card.player.peer_id == multiplayer.get_unique_id():
			card_texture_node.texture = card.card_front_texture
			if card.card_data.text:
				text_container_node.visible = true
				title_node.text = card.card_data.clabel
				text_node.text = card.card_data.text
			else:
				text_container_node.visible = false
				
		else:
			card_texture_node.texture = card.card_back_texture
			text_container_node.visible = false
