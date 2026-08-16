using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EWB24Liberator : EWCardLogic
{
    private bool QualifyingUnitExists() =>
        FactionState.ForEnum(Faction).ActiveUnitIds.ToUnitStates()
            .Where(us => us.Type == UnitType.ARMY)
            .Any(us => PathFindingService.IsWithinGeographicDistance(us.CountryId, (int)Country.Italy, 2));

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new CardStep(this, async() => {
                ForceDiscardCardsChangeEvent discardEvent = BuildChangeEvent(new ForceDiscardCardsChangeEvent(Faction, Faction.ITALY, 4));
                discardEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(discardEvent);
            })
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(QualifyingUnitExists), this))
            .WithGuidance("Italy must discard 4 cards")
        };
    }
}