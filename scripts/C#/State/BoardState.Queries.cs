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

    /// <summary>
    /// A strait belongs to its controlling country's team, and to the Allies while that is empty — unless
    /// a card in play decides it (<see cref="ForcedStraitController"/>).
    /// </summary>
    public FactionTeam StraitController(StraightState strait)
    {
        if (ForcedStraitController(strait) is { } forced) return forced;
        FactionTeam team = OccupyingTeam(strait.ControllingCountryState);
        return team == FactionTeam.NONE ? FactionTeam.ALLIES : team;
    }

    /// <summary>The team an IStraitControlModifier card gives <paramref name="strait"/>, or null when none has a say (always, in the base game).</summary>
    public FactionTeam? ForcedStraitController(StraightState strait)
    {
        foreach (IStraitControlModifier modifier in Modifiers<IStraitControlModifier>())
            if (modifier.StraitControl(strait) is { } team) return team;
        return null;
    }

    public bool IsStraitControlledBy(StraightState strait, Faction faction) =>
        StraitController(strait) == StaticGameData.FactionTeamForFaction(faction);

    /// <summary>
    /// The neighbours a faction can reach from a country: all of them, less any strait the enemy holds,
    /// plus any an IAdjacencyModifier card adds for it (none in the base game).
    /// </summary>
    public List<int> AdjacentCountryIds(Faction faction, CountryState country)
    {
        List<int> ids = MapAdjacentCountryIds(faction, country);
        foreach (IAdjacencyModifier modifier in Modifiers<IAdjacencyModifier>())
            foreach (int id in modifier.ExtraAdjacentCountryIds(faction, country))
                if (!ids.Contains(id)) ids.Add(id);
        return ids;
    }

    private List<int> MapAdjacentCountryIds(Faction faction, CountryState country)
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

    /// <summary>
    /// The faction's Armies and Navies on the board, in ownership order. Air Forces are left out: they
    /// never occupy, supply, battle or score, so every rule that walks a faction's units means its
    /// ground units — see <see cref="ActiveAirForceIds"/> for the others.
    /// </summary>
    public List<int> ActiveUnitIds(Faction faction)
    {
        List<int> active = new();
        foreach (int unitId in FactionState.ForEnum(faction).AllUnits)
        {
            UnitState unit = UnitState.ForId(unitId);
            if (!unit.IsAirForce && CountryOf(unit) >= 0) active.Add(unitId);
        }
        return active;
    }

    // ── Air Forces ──────────────────────────────────────────────────────────
    //
    // An Air Force stands in a space with its own faction's Army or Navy, at most one per faction per
    // space. It is NOT in CountryRecord.Units, which holds the ground piece that occupies the space, so
    // occupancy, battle targets, supply paths and scoring never see it; where it is comes from its own
    // UnitRecord.CountryId, which the sync hash and snapshots already cover.

    /// <summary>The faction's Air Forces on the board, in ownership order.</summary>
    public List<int> ActiveAirForceIds(Faction faction)
    {
        List<int> active = new();
        FactionState factionState = FactionState.ForEnum(faction);
        if (factionState == null) return active;
        foreach (int unitId in factionState.AllUnits)
        {
            UnitState unit = UnitState.ForId(unitId);
            if (unit.IsAirForce && CountryOf(unit) >= 0) active.Add(unitId);
        }
        return active;
    }

    /// <summary>The faction's Air Force in <paramref name="country"/>, or -1.</summary>
    public int AirForceIn(Faction faction, CountryState country)
    {
        foreach (int unitId in ActiveAirForceIds(faction))
            if (CountryOf(UnitState.ForId(unitId)) == country.Id) return unitId;
        return -1;
    }

    /// <summary>Every Air Force in <paramref name="country"/>, of any faction.</summary>
    public List<int> AirForcesIn(CountryState country) => StaticGameData.BoardFactions
        .Select(faction => AirForceIn(faction, country))
        .Where(unitId => unitId >= 0)
        .ToList();

    /// <summary>The faction has an Army or Navy here, and it is in supply: what an Air Force needs to stand here.</summary>
    public bool HasSuppliedGroundUnit(Faction faction, CountryState country)
        => UnitsIn(country).TryGetValue(faction, out int unitId) && InSupply(UnitState.ForId(unitId));

    /// <summary>Where the faction could deploy an Air Force: with its own supplied Army or Navy, and none of its Air Forces yet.</summary>
    public bool CanDeployAirForce(Faction faction, CountryState country)
        => HasSuppliedGroundUnit(faction, country) && AirForceIn(faction, country) < 0;

    /// <summary>
    /// Where an Air Force left without its own Army or Navy may go: an adjacent space with one of its
    /// faction's supplied Armies or Navies, and none of its Air Forces. Empty means it is lost.
    /// </summary>
    public List<int> RepositionTargets(UnitState airForce) => AdjacentCountryStates(airForce.Faction, CountryStateOf(airForce))
        .Where(country => CanDeployAirForce(airForce.Faction, country))
        .Select(country => country.Id)
        .ToList();

    /// <summary>
    /// The faction's supplied Air Forces in spaces adjacent to <paramref name="country"/>: what may pay
    /// for an air attack on it. An Air Force is supplied when the Army or Navy it stands with is.
    /// </summary>
    public List<int> SuppliedAirForcesAdjacentTo(Faction faction, CountryState country)
    {
        HashSet<int> adjacent = new(AdjacentCountryIds(faction, country));
        return ActiveAirForceIds(faction)
            .Where(unitId => adjacent.Contains(CountryOf(UnitState.ForId(unitId))) && InSupply(UnitState.ForId(unitId)))
            .ToList();
    }

    /// <summary>The faction's Air Forces on the board whose Army or Navy is in supply.</summary>
    public List<int> SuppliedAirForceIds(Faction faction) =>
        ActiveAirForceIds(faction).Where(unitId => InSupply(UnitState.ForId(unitId))).ToList();

    /// <summary>Every space the faction could deploy an Air Force to now. See <see cref="CanDeployAirForce"/>.</summary>
    public List<int> AirDeployTargets(Faction faction) => CountryState.AllCountryStates
        .Where(country => CanDeployAirForce(faction, country))
        .Select(country => country.Id)
        .ToList();

    /// <summary>
    /// Where a marshal may take <paramref name="airForce"/>: any space with its faction's supplied Army or
    /// Navy and none of its other Air Forces. Its own space counts: a marshal may stay put to trigger a reaction.
    /// </summary>
    public List<int> MarshalTargets(UnitState airForce) => CountryState.AllCountryStates
        .Where(country => HasSuppliedGroundUnit(airForce.Faction, country)
                          && AirForceIn(airForce.Faction, country) is var there && (there < 0 || there == airForce.Id))
        .Select(country => country.Id)
        .ToList();

    /// <summary>
    /// The spaces adjacent to one of the faction's supplied Air Forces that hold no Air Force of its own team:
    /// what its Air Superiority may target. A space with no Air Force at all counts, to trigger a reaction.
    /// </summary>
    public List<int> AirSuperiorityTargets(Faction faction) => SuppliedAirForceIds(faction)
        .SelectMany(unitId => AdjacentCountryIds(faction, CountryStateOf(UnitState.ForId(unitId))))
        .Distinct()
        .Where(countryId => !HasFriendlyAirForce(faction, CountryState.ForId(countryId)))
        .OrderBy(countryId => countryId)
        .ToList();

    private bool HasFriendlyAirForce(Faction faction, CountryState country)
        => AirForcesIn(country).Count > EnemyAirForcesIn(faction, country).Count;

    /// <summary>The Air Forces in <paramref name="country"/> belonging to the faction's enemies, minor factions included.</summary>
    public List<int> EnemyAirForcesIn(Faction faction, CountryState country) => AirForcesIn(country)
        .Where(unitId => StaticGameData.FactionTeamForFaction(UnitState.ForId(unitId).Faction) != StaticGameData.FactionTeamForFaction(faction))
        .ToList();

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
    public List<int> SupplyCountryIds(Faction faction) => SupplySpaceIds(faction, new List<Faction> { faction });

    /// <summary>The supply spaces <paramref name="faction"/> traces to: those held by any <see cref="SupplyLinkFactions"/> country, the same as SupplyCountryIds in the base game.</summary>
    public List<int> SupplySourceIds(Faction faction) => SupplySpaceIds(faction, SupplyLinkFactions(faction));

    /// <summary>The countries whose pieces <paramref name="faction"/> traces supply through: itself, plus any an ISupplyShareModifier card adds.</summary>
    public List<Faction> SupplyLinkFactions(Faction faction)
    {
        var factions = new List<Faction> { faction };
        foreach (Faction owner in StaticGameData.BoardFactions)
            if (owner != faction && Modifiers<ISupplyShareModifier>().Any(m => m.SharesSupply(faction, owner)))
                factions.Add(owner);
        return factions;
    }

    private List<int> SupplySpaceIds(Faction faction, List<Faction> holders)
    {
        var response = new List<int>();
        foreach (CountryState countryState in CountryState.AllCountryStates)
        {
            if (IsSupplySpace(countryState, faction) && holders.Any(UnitsIn(countryState).ContainsKey))
                response.Insert(0, countryState.Id);
        }
        return response;
    }

    /// <summary>
    /// Whether a space is a Supply space for a faction, both to trace supply to and to score its star:
    /// the map's star or an ISupplySpaceModifier card's, unless an ISupplyBlockModifier card denies it.
    /// </summary>
    public bool IsSupplySpace(CountryState country, Faction faction) =>
        (country.IsSupply || Modifiers<ISupplySpaceModifier>().Any(m => m.AddsSupplySpace(country.Id, faction)))
        && !Modifiers<ISupplyBlockModifier>().Any(m => m.BlocksSupply(country.Id, faction));

    // ── Placement ───────────────────────────────────────────────────────────

    /// <summary>A country's Home space: its printed one, unless an IHomeSpaceModifier card in play moves it (never in the base game).</summary>
    public CountryState HomeSpaceOf(Faction faction)
    {
        foreach (IHomeSpaceModifier modifier in Modifiers<IHomeSpaceModifier>())
            if (modifier.HomeSpace(this, faction) is { } moved) return moved;
        return StaticGameData.FactionDataMap[faction].HomeSpaceCountryState;
    }

    public bool IsHomeSpace(Faction faction, CountryState country) => HomeSpaceOf(faction).Id == country.Id;

    /// <summary>
    /// Recruitable, and off the home space an adjacent supplied unit, and at sea a harbour. A faction may
    /// build where it already stands — see CountryState.CanBuild for why that is not excluded.
    /// </summary>
    public bool CanBuild(Faction faction, CountryState country) =>
        CanRecruit(faction, country) &&
        (!IsHomeSpace(faction, country) ? HasAdjacentSuppliedUnit(faction, country) : true) &&
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
