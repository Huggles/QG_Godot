using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class StatusAbundantResources : StatusCardLogic, IVPModifier
{
    private static readonly List<Country> scoringCountries = [Country.Ukraine, Country.Kazakhstan, Country.Russia];

    /// <summary>The pieces that score. Read by both the VP count and <see cref="Targets"/>.</summary>
    private List<UnitState> ScoringUnits =>
        FactionState.ForEnum(Faction).ActiveUnitIds.ToUnitStates()
            .Where(unitState => scoringCountries.Contains(unitState.CountryState.Country))
            .ToList();

    /// <summary>Hovering shows which pieces are currently earning this card its points.</summary>
    public override TargetSet Targets() => TargetSet.Units(ScoringUnits);

    public virtual VPEntry AddVictoryPoints()
    {
        return new VPEntry(ScoringUnits.Count, "armies on Ukraine, Kazakhstan and/or Russia");
    }

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> { Condition.Build(new Condition.IsVictoryPointStep(), this) };
    }
}
