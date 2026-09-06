using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class StatusBalkanResources : StatusCardLogic, IVPModifier
{
    /// <summary>The Italian Armies in the Balkans. The card scores at most one point however many
    /// there are, but the preview shows them all — they are what is holding the point.</summary>
    private List<UnitState> ScoringUnits =>
        FactionState.ForEnum(Faction).ActiveUnitIds.ToUnitStates()
            .Where(us => us.CountryState.Country == Country.Balkans && us.Type == UnitType.ARMY)
            .ToList();

    public override TargetSet Targets() =>
        TargetSet.Countries(new List<Country> { Country.Balkans }).Plus(TargetSet.Units(ScoringUnits));

    public virtual VPEntry AddVictoryPoints()
    {
        return new VPEntry(ScoringUnits.Count > 0 ? 1 : 0, "an Italian Army in the Balkans");
    }

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> { Condition.Build(new Condition.IsVictoryPointStep(), this) };
    }
}