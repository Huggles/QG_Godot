using System;
using System.Collections.Generic;
using Godot;

public partial class StatusFlyingFortresses : StatusCardLogic, IDiscardModifier
{
    public int ModifyDiscard(ForceDiscardCardsChangeEvent discardEvent)
    {
        if (discardEvent.SourceCardState == null) return 0;
        bool isUSEWCard = discardEvent.SourceCardState.CardData.CardType == CardType.ECONOMIC_WARFARE
            && discardEvent.SourceCardState.Faction == Faction;
        if (!isUSEWCard) return 0;
        return StaticGameData.FactionTeamForFaction(discardEvent.TargetFaction) == FactionTeam.AXIS ? 2 : 0;
    }
}