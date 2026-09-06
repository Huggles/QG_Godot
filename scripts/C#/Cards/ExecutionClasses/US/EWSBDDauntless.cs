using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EWSBDDauntless : EWCardLogic
{
    /// <summary>
    /// The US Navies close enough to Japan to arm this card. Reuses the same
    /// PathFindingService range test the condition always did — extracted so <see cref="Targets"/>
    /// can show WHICH piece is arming it, which is the one thing the card text does not tell you.
    /// </summary>
    private List<UnitState> QualifyingUnits() =>
        FactionState.ForEnum(Faction).ActiveUnitIds.ToUnitStates()
            .Where(us => us.Type == UnitType.NAVY)
            .Where(us => PathFindingService.IsWithinGeographicDistance(us.CountryId, (int)Country.Japan, 2))
            .ToList();

    private bool QualifyingUnitExists() => QualifyingUnits().Count > 0;

    /// <summary>The pieces arming this card, and the home space they are bombing.</summary>
    public override TargetSet Targets() =>
        TargetSet.Units(QualifyingUnits())
            .Plus(TargetSet.Countries(new List<Country> { Country.Japan }));

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new CardStep(this, async() => {
                ForceDiscardCardsChangeEvent discardEvent = BuildChangeEvent(new ForceDiscardCardsChangeEvent(Faction, Faction.JAPAN, 4));
                discardEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(discardEvent);
            })
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(QualifyingUnitExists), this))
            .WithGuidance("Japan must discard 4 cards")
        };
    }
}