using Godot;
using System;

public partial class CardData : DataObject
{
    public string UniqueName {get; set;}
    public string Label {get; set;}
    public string Text {get; set;}
    public string Type {get; set;}
    public string ExecutionClass {get; set;}

    public CardType CardType 
    {
        get
        {
            switch (Type)
            {
                case "BUILD_ARMY"   : return CardType.BUILD_ARMY;
                case "BUILD_NAVY"   : return CardType.BUILD_NAVY;
                case "LAND_BATTLE"  : return CardType.LAND_BATTLE;
                case "SEA_BATTLE"   : return CardType.SEA_BATTLE;
                case "STATUS"       : return CardType.STATUS;
                case "RESPONSE"     : return CardType.RESPONSE;
                case "EVENT"        : return CardType.EVENT;
                case "EW"           : return CardType.ECONOMIC_WARFARE;
                default             : return CardType.NONE;
            }
        }
    }

    // public Texture2D CardFrontTexture
    // {
    //     get
    //     {
    //         switch (CardData.Type)
    //         {
    //             case "BUILD_ARMY": return GD.Load<Texture2D>(FactionData.CardFrontBuildArmyTexture);
    //             case "BUILD_NAVY": return GD.Load<Texture2D>(FactionData.CardFrontBuildNavyTexture);
    //             case "LAND_BATTLE": return GD.Load<Texture2D>(FactionData.CardFrontLandBattleTexture);
    //             case "SEA_BATTLE": return GD.Load<Texture2D>(FactionData.CardFrontSeaBattleTexture);
    //             case "STATUS": return GD.Load<Texture2D>(FactionData.CardFrontStatusTexture);
    //             case "RESPONSE": return GD.Load<Texture2D>(FactionData.CardFrontResponseTexture);
    //             case "EVENT": return GD.Load<Texture2D>(FactionData.CardFrontEventTexture);
    //             case "EW": return GD.Load<Texture2D>(FactionData.CardFrontEwTexture);
    //             default: return null;
    //         }
    //     }
    // }

    // public Texture2D CardBackTexture => GD.Load<Texture2D>(FactionData.CardBackTexture);
}
