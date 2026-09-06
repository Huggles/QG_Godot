using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class StatusAntiCommunistSentiment : StatusCardLogic, IVPModifier
{
    private static readonly List<Country> scoringCountries = [Country.Ukraine, Country.Russia];

    /// <summary>The pieces that score. Read by both the VP count and <see cref="Targets"/>.</summary>
    private List<UnitState> ScoringUnits =>
        FactionState.ForEnum(Faction).ActiveUnitIds.ToUnitStates()
            .Where(unitState => scoringCountries.Contains(unitState.CountryState.Country))
            .ToList();

    public override TargetSet Targets() => TargetSet.Units(ScoringUnits);

    public virtual VPEntry AddVictoryPoints()
    {
        return new VPEntry(ScoringUnits.Count, $"{FactionState.ForEnum(Faction).FactionData.FactionAdjactiveLabel} armies in {CountryState.ForEnum(scoringCountries[0]).Label} and {CountryState.ForEnum(scoringCountries[1]).Label}");
    }

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> { Condition.Build(new Condition.IsVictoryPointStep(), this) };
    }
}