using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class StatusSwedishIronOre : StatusCardLogic, IVPModifier
{
    /// <summary>The two pieces that score, either of which may be absent. Read by both the VP count
    /// and <see cref="Targets"/>, so the preview shows exactly what is earning the points.</summary>
    private List<UnitState> ScoringUnits =>
        FactionState.ForEnum(Faction).ActiveUnitIds.ToUnitStates()
            .Where(u => (u.CountryState.Country == Country.BalticSea && u.Type == UnitType.NAVY)
                     || (u.CountryState.Country == Country.Scandinavia && u.Type == UnitType.ARMY))
            .ToList();

    public override TargetSet Targets() => TargetSet.Units(ScoringUnits);

    public virtual VPEntry AddVictoryPoints()
    {
        // One point per QUALIFYING SPACE, not per piece: two navies in the Baltic still score one.
        int score = ScoringUnits.Select(u => u.CountryState.Country).Distinct().Count();
        return new VPEntry(score, "a navy in the Baltic Sea and army in Scandinavia");
    }

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> { Condition.Build(new Condition.IsVictoryPointStep(), this) };
    }
}
