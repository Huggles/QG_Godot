class_name DeckData
extends Object


var name:String
var clabel:String
var faction:String
var deck_cards:Array[CardBase]

func _init(deck:Dictionary):
	self.faction = deck.faction	
	for card:Dictionary in deck.cards:		
		var card_name = card.get("card_name")
		var card_number = card.get("number")
		for n in card_number:
			var faction_data = GameManager.game_mode.faction_data_map[faction]
			var card_base = GameManager.game_mode.card_data_map[card_name].get_card(faction_data)
			deck_cards.push_back(card_base)
			
			
		
