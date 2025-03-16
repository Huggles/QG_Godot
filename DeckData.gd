class_name DeckData
extends Object


var cards:Array

var faction:String	
var faction_enum:Enum.Faction:
	get: return Enum.Faction.get(faction)	


func _init(_deck:Dictionary):
	self.faction = _deck.faction	
	self.cards = _deck.cards	
