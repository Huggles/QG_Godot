using Godot;
using System;

public partial class CardData : DataObject
{
    public string UniqueName {get; set;}
    public string Label {get; set;}
    public string Text {get; set;}
    public string Type {get; set;}
    public string ExecutionClass { get; set; }
    public ICondition PlayCondition { get; set; }

    public CardType CardType
    {
        get
        {
            switch (Type)
            {
                case "BUILD_ARMY": return CardType.BUILD_ARMY;
                case "BUILD_NAVY": return CardType.BUILD_NAVY;
                case "LAND_BATTLE": return CardType.LAND_BATTLE;
                case "SEA_BATTLE": return CardType.SEA_BATTLE;
                case "STATUS": return CardType.STATUS;
                case "RESPONSE": return CardType.RESPONSE;
                case "EVENT": return CardType.EVENT;
                case "EW": return CardType.ECONOMIC_WARFARE;
                default: return CardType.NONE;
            }
        }
    }
}
