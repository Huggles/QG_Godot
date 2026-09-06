using System;
using System.Collections.Generic;
using Godot;

public partial class StatusJetFighters : StatusCardLogic, IDiscardModifier
{
    // No Targets() override: this reduces what an EW card costs you in discards. A deck has no
    // place on the board, so there is nothing to light up.

    public int ModifyDiscard(ForceDiscardCardsChangeEvent discardEvent)
    {
        bool isEW = discardEvent.SourceCardState.CardData.CardType == CardType.ECONOMIC_WARFARE;
        bool targetIsMe = discardEvent.TargetFaction == Faction;
        if (isEW && targetIsMe)
            return -3;
        return 0;
    }
}