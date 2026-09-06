using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class StatusWolfPacks : StatusCardLogic, IDiscardModifier
{
    /// <summary>
    /// The Army in Scandinavia that lifts this card from 2 discards to 3. The discard itself lands on
    /// a deck and has no board place, so the piece that sets the rate is the only thing worth showing.
    /// </summary>
    private List<UnitState> BonusUnits =>
        FactionState.ForEnum(Faction).ActiveUnitIds.ToUnitStates()
            .Where(u => u.Type == UnitType.ARMY && u.CountryState.Country == Country.Scandinavia)
            .ToList();

    public override TargetSet Targets() => TargetSet.Units(BonusUnits);

    public int ModifyDiscard(ForceDiscardCardsChangeEvent discardEvent)
    {
        if(discardEvent.SourceCardState == null) return 0; // If SourceCardState is null, this discard event is not caused by a card play and should not be modified
        bool isSubmarineEW = discardEvent.SourceCardState.CardData.CardType == CardType.ECONOMIC_WARFARE
            && discardEvent.SourceCardState.Faction == Faction
            && discardEvent.SourceCardState.CardData.Label.Contains("Submarines");
        if (!isSubmarineEW) return 0;

        return BonusUnits.Count > 0 ? 3 : 2;
    }
}