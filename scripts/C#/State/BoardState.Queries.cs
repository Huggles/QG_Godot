using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The rule questions a board can answer about itself: occupancy, adjacency, supply, buildability,
/// battle targets, the unit pool and the card piles. The state objects' own methods (CountryState.CanBuild
/// and friends) forward here with <see cref="Live"/>, so the live game and a fork share one definition.
///
/// Identity and topology come from the state objects — a CountryState is "the country" — and every
/// changing value is read from this board through <see cref="Of(CountryState)"/> and its overloads.
/// </summary>
public sealed partial class BoardState
{
    // ── Records by identity ─────────────────────────────────────────────────
    // The live board short-circuits to the object's own record, which keeps the calculator's hot
    // paths at the field reads they were before.

    public CountryRecord Of(CountryState country) => _source != null ? country.Record : _countries[country.Id];
    public UnitRecord Of(UnitState unit) => _source != null ? unit.Record : _units[unit.Id];
    public CardRecord Of(CardState card) => _source != null ? card.Record : _cards.GetOrAdd(card.Id, _ => card.Record.Clone());
    public StraitRecord Of(StraightState strait) => _source != null ? strait.Record : _straits[strait.ControllingCountryId];
    public StepRecord Of(CardStep step) => _source != null ? step.Record : _steps.GetOrAdd(step.Id, _ => step.Record.Clone());

    // ── Occupancy ───────────────────────────────────────────────────────────

    public Dictionary<Faction, int> UnitsIn(CountryState country) => Of(country).Units;

    /// <summary>
    /// The team holding a country. A country locks to its first occupier's team, so any occupant
    /// answers it, and this avoids materialising the occupant list on the CanBuild hot path.
    /// </summary>
    public FactionTeam OccupyingTeam(CountryState country)
    {
        foreach (Faction occupant in Of(country).Units.Keys)
            return StaticGameData.FactionTeamForFaction(occupant);
        return FactionTeam.NONE;
    }

    public bool IsEmpty(CountryState country) => Of(country).Units.Count == 0;
    public bool HasUnit(Faction faction, CountryState country) => Of(country).Units.ContainsKey(faction);

    public bool CanRecruit(Faction faction, CountryState country)
    {
        FactionTeam occupying = OccupyingTeam(country);
        return occupying == FactionTeam.NONE || occupying == StaticGameData.FactionTeamForFaction(faction);
    }

    public bool HasHarbor(Faction faction, CountryState country)
    {
        FactionTeam team = StaticGameData.FactionTeamForFaction(faction);
        return country.ConnectedCountryStates.Any(connected => connected.IsLand && OccupyingTeam(connected) == team);
    }

    // ── Adjacency ───────────────────────────────────────────────────────────

    /// <summary>A strait belongs to its controlling country's team, and to the Allies while that is empty.</summary>
    public FactionTeam StraitController(StraightState strait)
    {
        FactionTeam team = OccupyingTeam(strait.ControllingCountryState);
        return team == FactionTeam.NONE ? FactionTeam.ALLIES : team;
    }

    public bool IsStraitControlledBy(StraightState strait, Faction faction) =>
        StraitController(strait) == StaticGameData.FactionTeamForFaction(faction);

    /// <summary>The neighbours a faction can reach from a country: all of them, less any strait the enemy holds.</summary>
    public List<int> AdjacentCountryIds(Faction faction, CountryState country)
    {
        List<StraightState> straits = country.ControlledByStraightStates;
        if (straits.Count == 0) return new List<int>(country.ConnectedCountryIds);

        return country.ConnectedCountryStates
            .Where(ccs => !straits.Any(ss => !IsStraitControlledBy(ss, faction) && ss.IsForIds(country.Id, ccs.Id)))
            .Select(ccs => ccs.Id).ToList();
    }

    public List<CountryState> AdjacentCountryStates(Faction faction, CountryState country) =>
        CountryState.ForIds(AdjacentCountryIds(faction, country));

    // ── Units and supply ────────────────────────────────────────────────────

    public int CountryOf(UnitState unit) => Of(unit).CountryId;

    /// <summary>The country a unit stands in on this board, or null while it is in the pool.</summary>
    public CountryState CountryStateOf(UnitState unit) => CountryOf(unit) is >= 0 and var id ? CountryState.ForId(id) : null;

    /// <summary>The faction's units on the board, as objects.</summary>
    public List<UnitState> ActiveUnits(Faction faction) => UnitState.ForIds(ActiveUnitIds(faction));
    public bool IsDeployed(UnitState unit) => Of(unit).CountryId >= 0;

    /// <summary>In supply as of this board's last tag pass, or granted supply for the turn.</summary>
    public bool InSupply(UnitState unit)
    {
        UnitRecord record = Of(unit);
        return record.Tags.Has(Tag.InSupply, unit.Faction) || record.SuppliedForTurn;
    }

    public bool ImmuneForTurn(UnitState unit) => Of(unit).ImmuneForTurn;

    /// <summary>The faction's units on the board, in ownership order.</summary>
    public List<int> ActiveUnitIds(Faction faction)
    {
        List<int> active = new();
        foreach (int unitId in FactionState.ForEnum(faction).AllUnits)
            if (CountryOf(UnitState.ForId(unitId)) >= 0) active.Add(unitId);
        return active;
    }

    public List<int> SuppliedUnitIds(Faction faction) =>
        ActiveUnitIds(faction).Where(id => InSupply(UnitState.ForId(id))).ToList();

    public List<int> UnsuppliedUnitIds(Faction faction) =>
        ActiveUnitIds(faction).Where(id => !InSupply(UnitState.ForId(id))).ToList();

    public List<int> AdjacentUnits(Faction faction, CountryState country) =>
        AdjacentCountryStates(faction, country).Where(ccs => UnitsIn(ccs).ContainsKey(faction)).Select(ccs => UnitsIn(ccs)[faction]).ToList();

    public List<int> AdjacentSuppliedUnits(Faction faction, CountryState country) =>
        UnitState.ForIds(AdjacentUnits(faction, country)).Where(InSupply).Select(unit => unit.Id).ToList();

    /// <summary>
    /// Whether ANY adjacent unit of this faction is in supply — the question CanBuild asks. Short-circuits
    /// rather than building <see cref="AdjacentSuppliedUnits"/>: it usually resolves on the first neighbour.
    /// </summary>
    public bool HasAdjacentSuppliedUnit(Faction faction, CountryState country) => AdjacentCountryIds(faction, country)
        .Any(id => CountryState.ForId(id) is { } neighbour
                   && UnitsIn(neighbour).TryGetValue(faction, out int unitId)
                   && (UnitState.ForId(unitId) is { } unit && InSupply(unit)));

    /// <summary>The supply spaces a faction holds that no modifier blocks, as GameAPI.GetSupplyCountryIds lists them.</summary>
    public List<int> SupplyCountryIds(Faction faction)
    {
        var response = new List<int>();
        foreach (CountryState countryState in CountryState.AllCountryStates)
        {
            bool blocked = Modifiers<ISupplyBlockModifier>().Any(m => m.BlocksSupply(countryState.Id, faction));
            if (!blocked && countryState.IsSupply && UnitsIn(countryState).ContainsKey(faction))
                response.Insert(0, countryState.Id);
        }
        return response;
    }

    // ── Placement ───────────────────────────────────────────────────────────

    /// <summary>
    /// Recruitable, and off the home space an adjacent supplied unit, and at sea a harbour. A faction may
    /// build where it already stands — see CountryState.CanBuild for why that is not excluded.
    /// </summary>
    public bool CanBuild(Faction faction, CountryState country) =>
        CanRecruit(faction, country) &&
        (!country.IsHomeSpace(faction) ? HasAdjacentSuppliedUnit(faction, country) : true) &&
        (country.IsSea ? HasHarbor(faction, country) : true);

    public int AvailableUnitCount(Faction faction, UnitType unitType)
    {
        FactionState factionState = FactionState.ForEnum(faction);
        if (factionState == null) return 0;
        return UnitState.ForIds(factionState.AllUnits).Count(unit => unit.Type == unitType && !IsDeployed(unit));
    }

    /// <summary>The piece a deploy of this type would use: the first undeployed one in creation order, or -1.</summary>
    public int NextAvailableUnit(Faction faction, UnitType unitType)
    {
        foreach (UnitState unit in UnitState.ForIds(FactionState.ForEnum(faction).AllUnits))
            if (!IsDeployed(unit) && unit.Type == unitType) return unit.Id;
        return -1;
    }

    // ── Battle ──────────────────────────────────────────────────────────────

    public bool HasAttackableUnit(Faction faction, CountryState country) =>
        OccupyingTeam(country) == StaticGameData.OpponentFactionTeamForFaction(faction);

    public bool CanAttack(Faction faction, CountryState country) =>
        HasAdjacentSuppliedUnit(faction, country) && HasAttackableUnit(faction, country);

    public bool CanAttackWhenEmpty(Faction faction, CountryState country) =>
        HasAdjacentSuppliedUnit(faction, country) && IsEmpty(country);

    public List<BattleTarget> AdjacentBattleTargets(Faction attackingFaction, CountryType countryType, CountryState country)
    {
        List<BattleTarget> targets = new List<BattleTarget>();
        List<BattleTarget> targetableUnitIds = AdjacentCountryStates(attackingFaction, country)
            .Where(connected => connected.Type == countryType && CanAttack(attackingFaction, connected)).ToList()
            .SelectMany(withUnits => UnitsIn(withUnits).Values.Where(unitId => !ImmuneForTurn(UnitState.ForId(unitId)))).Distinct().ToList()
            .Map(unitId => new BattleTarget(unitId, TargetType.UNIT));
        List<BattleTarget> targetableEmptyCountriesIds = AdjacentCountryStates(attackingFaction, country)
            .Where(connected => connected.Type == countryType && CanAttackWhenEmpty(attackingFaction, connected)).ToList().ToCountryIds()
            .Map(countryId => new BattleTarget(countryId, TargetType.COUNTRY));
        targets.AddRange(targetableUnitIds);
        targets.AddRange(targetableEmptyCountriesIds);
        return targets;
    }

    public List<BattleTarget> BattleTargets(Faction attackingFaction, CountryState country)
    {
        if (!HasAdjacentSuppliedUnit(attackingFaction, country) || OccupyingTeam(country) == StaticGameData.FactionTeamForFaction(attackingFaction))
            return [];
        if (IsEmpty(country))
            return [new BattleTarget(country.Id, TargetType.COUNTRY)];
        return UnitsIn(country).Values.ToList().Map(unitId => new BattleTarget(unitId, TargetType.UNIT));
    }

    // ── Tags ────────────────────────────────────────────────────────────────

    public List<CountryState> CountriesWithTag(Tag tag, Faction faction) =>
        CountryState.AllCountryStates.Where(cs => Of(cs).Tags.Has(tag, faction)).ToList();

    public List<CountryState> CountriesWithTags(Tag[] tags, Faction faction) =>
        CountryState.AllCountryStates.Where(cs => tags.All(tag => Of(cs).Tags.Has(tag, faction))).ToList();

    public List<CountryState> BuildableLand(Faction faction) => CountriesWithTags(new[] { Tag.Buildable, Tag.LandCountry }, faction);
    public List<CountryState> BuildableSea(Faction faction) => CountriesWithTags(new[] { Tag.Buildable, Tag.SeaCountry }, faction);
    public List<CountryState> RecruitableLand(Faction faction) => CountriesWithTags(new[] { Tag.Recruitable, Tag.LandCountry }, faction);
    public List<CountryState> RecruitableSea(Faction faction) => CountriesWithTags(new[] { Tag.Recruitable, Tag.SeaCountry }, faction);
    public List<CountryState> AttackableLand(Faction faction) => CountriesWithTags(new[] { Tag.Attackable, Tag.LandCountry }, faction);
    public List<CountryState> AttackableSea(Faction faction) => CountriesWithTags(new[] { Tag.Attackable, Tag.SeaCountry }, faction);

    public List<int> BuildableLandIds(Faction faction) => BuildableLand(faction).Select(c => c.Id).ToList();
    public List<int> BuildableSeaIds(Faction faction) => BuildableSea(faction).Select(c => c.Id).ToList();
    public List<int> RecruitableLandIds(Faction faction) => RecruitableLand(faction).Select(c => c.Id).ToList();
    public List<int> RecruitableSeaIds(Faction faction) => RecruitableSea(faction).Select(c => c.Id).ToList();
    public List<int> AttackableLandIds(Faction faction) => AttackableLand(faction).Select(c => c.Id).ToList();
    public List<int> AttackableSeaIds(Faction faction) => AttackableSea(faction).Select(c => c.Id).ToList();

    public List<UnitState> UnitsWithTag(Tag tag, Faction faction) =>
        UnitState.AllUnitStates.Where(us => Of(us).Tags.Has(tag, faction)).ToList();

    public List<UnitState> AttackableArmies(Faction faction) =>
        UnitState.AllUnitStates.Where(us => Of(us).Tags.Has(Tag.Attackable, faction) && us.Type == UnitType.ARMY).ToList();

    public List<UnitState> AttackableNavies(Faction faction) =>
        UnitState.AllUnitStates.Where(us => Of(us).Tags.Has(Tag.Attackable, faction) && us.Type == UnitType.NAVY).ToList();

    public List<int> AttackableArmyIds(Faction faction) => AttackableArmies(faction).Select(u => u.Id).ToList();
    public List<int> AttackableNavyIds(Faction faction) => AttackableNavies(faction).Select(u => u.Id).ToList();

    /// <summary>The countries the faction's units stand in, one per unit, in ownership order.</summary>
    public List<int> OccupiedCountryIds(Faction faction) =>
        ActiveUnitIds(faction).Select(id => CountryOf(UnitState.ForId(id))).ToList();

    // ── Cards ───────────────────────────────────────────────────────────────

    // Through the card: a Bulletin is always played and never discarded, whatever the piles say.
    public bool IsPlayed(CardState card) => card.IsPlayedOn(this);
    public bool IsDiscarded(CardState card) => card.IsDiscardedOn(this);
    public int ScoreOf(Faction faction) => ForFaction(faction).Score;
}
