using Godot;
using System;

public enum CardType
{     
    NONE,
    BUILD_ARMY, 
    BUILD_NAVY, 
    LAND_BATTLE, 
    SEA_BATTLE, 
    EVENT, 
    RESPONSE, 
    STATUS, 
    ECONOMIC_WARFARE,
    AIR_POWER,
    BOLSTER
}

public static class CardTypeRules
{
    /// <summary>Whether a card of this type can be played from hand as a card play. Air Power and Bolster cards never are, but can still be discarded.</summary>
    public static bool IsPlayedFromHand(this CardType cardType) => cardType is not (CardType.AIR_POWER or CardType.BOLSTER);
}
