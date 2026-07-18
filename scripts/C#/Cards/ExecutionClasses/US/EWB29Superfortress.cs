using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EWB29Superfortress : EWCardLogic
{
    private bool QualifyingUnitExists() =>
        FactionState.ForEnum(Faction).ActiveUnitIds.ToUnitStates()
            .Where(us => us.Type == UnitType.ARMY)
            .Any(us => PathFindingService.IsWithinGeographicDistance(us.CountryId, (int)Country.Germany, 3));

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new CardStep(this, async() => {
                ForceDiscardCardsChangeEvent discardEvent = BuildChangeEvent(new ForceDiscardCardsChangeEvent(Faction, Faction.GERMANY, 5));
                discardEvent.IsTrigger = true;
                return discardEvent;
            })
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(QualifyingUnitExists), this))
            .WithGuidance("Germany must discard 5 cards")
        };
    }
}