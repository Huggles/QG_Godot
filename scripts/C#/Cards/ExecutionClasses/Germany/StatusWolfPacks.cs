using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class StatusWolfPacks : StatusCardLogic, IDiscardModifier
{
    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> { Condition.Build(new Condition.IsVictoryPointStep(), this) };
    }

    public int ModifyDiscard(ForceDiscardCardsChangeEvent discardEvent)
    {
        if(discardEvent.SourceCardState == null) return 0; // If SourceCardState is null, this discard event is not caused by a card play and should not be modified
        bool isSubmarineEW = discardEvent.SourceCardState.CardData.CardType == CardType.ECONOMIC_WARFARE
            && discardEvent.SourceCardState.Faction == Faction
            && discardEvent.SourceCardState.CardData.Label.Contains("Submarines");
        if (!isSubmarineEW) return 0;

        bool armyInScandinavia = FactionState.ForEnum(Faction).ActiveUnitIds.ToUnitStates()
            .Any(u => u.Type == UnitType.ARMY && u.CountryState.Country == Country.Scandinavia);
        return armyInScandinavia ? 3 : 2;
    }
}