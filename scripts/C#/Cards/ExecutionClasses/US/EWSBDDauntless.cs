using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EWSBDDauntless : EWCardLogic
{
    private bool QualifyingUnitExists() =>
        FactionState.ForEnum(Faction).ActiveUnitIds.ToUnitStates()
            .Where(us => us.Type == UnitType.NAVY)
            .Any(us => PathFindingService.IsWithinGeographicDistance(us.CountryId, (int)Country.Japan, 2));

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new CardStep(this, async() => {
                ForceDiscardCardsChangeEvent discardEvent = BuildChangeEvent(new ForceDiscardCardsChangeEvent(Faction, Faction.JAPAN, 4));
                discardEvent.IsTrigger = true;
                return discardEvent;
            })
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(QualifyingUnitExists), this))
            .WithGuidance("Japan must discard 4 cards")
        };
    }
}