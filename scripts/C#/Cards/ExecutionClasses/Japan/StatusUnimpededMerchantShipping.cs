using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class StatusUnimpededMerchantShipping : StatusCardLogic, IVPModifier
{
    private static readonly List<Country> watchedCountries = [Country.Hawaii];
    private static readonly List<Faction> alliedFactions =
        [Faction.UNITED_KINGDOM, Faction.SOVIET, Faction.UNITED_STATES];

    /// <summary>
    /// The Allied pieces in Hawaii — the ones DENYING the point. This card scores on an absence, so
    /// what is worth showing is whatever is currently spoiling it; the space itself is reported too,
    /// so an empty Hawaii still lights up and reads as "this is earning".
    /// </summary>
    private List<UnitState> BlockingUnits =>
        alliedFactions
            .SelectMany(faction => FactionState.ForEnum(faction).ActiveUnitIds.ToUnitStates())
            .Where(u => watchedCountries.Contains(u.CountryState.Country))
            .ToList();

    public override TargetSet Targets() =>
        TargetSet.Countries(watchedCountries).Plus(TargetSet.Units(BlockingUnits));

    public virtual VPEntry AddVictoryPoints()
    {
        int score = BlockingUnits.Count > 0 ? 0 : 1;
        return new VPEntry(score, $"no Allied army in {CountryState.ForEnum(watchedCountries[0]).Label}");
    }

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> { Condition.Build(new Condition.IsVictoryPointStep(), this) };
    }
}