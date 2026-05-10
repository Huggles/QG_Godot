using System;
using System.Collections.Generic;
using Godot;

public partial class StatusJetFighters : StatusCardLogic, IDiscardModifier
{
    public int ModifyDiscard(DiscardCardsChangeEvent discardEvent)
    {
        bool isEW = discardEvent.SourceCardState.CardData.CardType == CardType.ECONOMIC_WARFARE;
        bool targetIsMe = discardEvent.TargetFaction == Faction;
        if (isEW && targetIsMe)
            return -3;
        return 0;
    }
}