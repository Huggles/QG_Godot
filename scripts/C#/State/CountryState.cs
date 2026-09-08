using Godot;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json.Serialization;

public partial class CountryState : StateObject
{
    [JsonIgnore] public CountryData StaticCountryData;  
    public string Name { get; set; }
    public string NameCamelCase { get; set; }
    public string Label { get; set; }
    public CountryType Type { get; set; }
    public bool IsSupply { get; set; }
    public StraightState StraightState;

    public Country Country { get { return (Country)Id; } }
    public List<string> Neighbors { get; set; }

    [JsonIgnore] public List<int> ConnectedCountryIds { get; set; } = new List<int>();

    /// <summary>
    /// This country's neighbours, resolved once.
    ///
    /// Map topology never changes after <see cref="InitNeighborCountryStateArray"/>, but this used to
    /// rebuild the whole list on every read — and it is read from inside CanBuild, which
    /// GameStateCalculator evaluates for every country of every faction after every ChangeEvent
    /// (~330,000 times in a full game, each read allocating). Caching it was ~40% of a headless run.
    /// Invalidated where the ids are assigned, so a re-init cannot leave this stale.
    /// </summary>
    [JsonIgnore] private List<CountryState> _connectedCountryStates;
    [JsonIgnore] public List<CountryState> ConnectedCountryStates
        => _connectedCountryStates ??= CountryState.ForIds(ConnectedCountryIds);

    public Dictionary<Faction, int> Units { get; set; } = new Dictionary<Faction, int>();

    public bool IsLand => Type == CountryType.LAND;
    public bool IsSea => Type == CountryType.SEA;

    public bool IsCountryEmpty => Units.Count == 0;
    public bool IsCountryFull => Units.Count == 3;

    public List<Faction> OccupyingFactions => Units.Keys.ToList();

    /// <summary>
    /// The team holding this country, without materialising <see cref="OccupyingFactions"/>.
    ///
    /// A country locks to the first occupier's team, so any occupant answers it. Worth avoiding the
    /// list: OccupyingTeam is read from CanRecruit and HasHarbor, which GameStateCalculator runs for
    /// every country of every faction after every ChangeEvent.
    /// </summary>
    private FactionTeam OccupyingTeamFast()
    {
        foreach (Faction occupant in Units.Keys)
            return StaticGameData.FactionTeamForFaction(occupant);
        return FactionTeam.NONE;
    }

    public FactionTeam OccupyingTeam => OccupyingTeamFast();

    [JsonIgnore] public CountryScene CountryScene => NodeUtilities.Instance.CountriesNode.GetChildren().ToList().Find(c => c.Name == Name) as CountryScene ?? throw new Exception($"Couldn't find CountryScene for country: {Name}");

    public CountryState(CountryData countryData)
    {
        StaticCountryData = countryData;
        Id = countryData.Number;
        Name = countryData.UniqueName;
        NameCamelCase = countryData.UniqueNameCamelCase;
        Label = countryData.Label;
        Type = countryData.Type == 0 ? CountryType.LAND : CountryType.SEA;
        IsSupply = countryData.IsSupply == "true";
        Neighbors = new List<string>(countryData.Neighbors);

        // Set country type tag (faction-independent, so use Faction.ALL)
        if (Type == CountryType.LAND)
            this.Tags.AddForAll(Tag.LandCountry);
        else
            this.Tags.AddForAll(Tag.SeaCountry);

        // Tag.Clickable's visual is driven by CountryScene.OnTagAdded/OnTagRemoved, and ONLY there.
        // It used to be driven from here as well, so SetClickable ran twice per tag and
        // CountrySpriteTextureRect.MouseLeftClickOnOpaque — a plain multicast delegate with no
        // duplicate detection — was subscribed twice, making one click emit CountryClicked twice.
        // Both consumers answer through TaskCompletionSource.TrySetResult, which is why the second
        // emit was swallowed and nothing ever looked wrong.
        //
        // CountryScene's copy is the one to keep: this class is state, that handler is paired with an
        // unsubscribe in _ExitTree, and Tag.Clickable now sits with Tag.RebuildTarget and
        // Tag.PreviewTarget, which were only ever handled there. The !GameContext.IsHeadless guard
        // this replaced matched exactly when countries are spawned at all — see
        // GodotWorldPresenter.SpawnCountries versus its headless no-op — so nothing is lost.
    }

    public void InitNeighborCountryStateArray()
    {
        ConnectedCountryIds = Neighbors
            .Select(neighbor => MultiplayerSession.Instance.GameState.CountryStateByName.TryGetValue(neighbor, out CountryState neighborState) 
                                ? neighborState.Id
                                : throw new Exception( $"Couldn't find: {neighbor} as neighbor of {Name}"))
            .ToList();

        // The two topology caches are derived from the ids just assigned.
        _connectedCountryStates = null;
        _controlledByStraightStates = null;
    }

    public List<StraightState> ControllingStraightStates => IsSea ? [] : ConnectedCountryStates
        .Where(ccs => GameAPI.StraightStateForNeighbors(Id, ccs.Id) != null && GameAPI.StraightStateForNeighbors(Id, ccs.Id).ControllingCountryState.Id == Id)
        .Select(ccs => GameAPI.StraightStateForNeighbors(Id, ccs.Id))
        .ToList();
    
    /// <summary>
    /// The straits that gate movement out of this sea space, resolved once.
    ///
    /// Which neighbours a strait sits between is fixed map topology — only who CONTROLS one changes,
    /// and that is read separately by <see cref="ControllingStraightStatesForFaction"/>. The old
    /// expression called GameAPI.StraightStateForNeighbors twice per neighbour, and that does a linear
    /// scan of every strait and allocates a list to return at most one of them. On the CanBuild path
    /// this ran hundreds of thousands of times a game.
    /// </summary>
    [JsonIgnore] private List<StraightState> _controlledByStraightStates;
    private List<StraightState> ControlledByStraightStates => _controlledByStraightStates ??=
        IsLand ? new List<StraightState>()
               : ConnectedCountryStates
                   .Where(ccs => ccs.IsSea)
                   .Select(ccs => GameAPI.StraightStateForNeighbors(Id, ccs.Id))
                   .Where(ss => ss != null)
                   .ToList();
    public List<StraightState> ControllingStraightStatesForFaction(Faction faction) => ControlledByStraightStates.Where(ss => ss.IsControlledByFaction(faction)).ToList();
    public List<StraightState> UncontrolledStraightStatesForFaction(Faction faction) => ControlledByStraightStates.Where(ss => !ss.IsControlledByFaction(faction)).ToList();
    public bool IsControlledByStraightState => ControlledByStraightStates.Count > 0;

    public List<int> AdjacentCountryIds(Faction faction) {
        // ControlledByStraightStates is now cached, so asking it directly costs nothing — the old code
        // computed it twice per call (once via IsControlledByStraightState, once via
        // UncontrolledStraightStatesForFaction) when it was still rebuilt from scratch each time.
        List<StraightState> straits = ControlledByStraightStates;
        if (straits.Count == 0) return new List<int>(ConnectedCountryIds);

        return ConnectedCountryStates
            .Where(ccs => !straits.Any(ss => !ss.IsControlledByFaction(faction) && ss.IsForIds(this.Id, ccs.Id)))
            .Select(ccs => ccs.Id).ToList();
    }
    

    public List<CountryState> AdjacentCountryStates(Faction faction) => CountryState.ForIds(AdjacentCountryIds(faction));

    public bool HasHarbor(Faction faction)
    {
        FactionTeam team = StaticGameData.FactionTeamForFaction(faction);
        return ConnectedCountryStates.Any(connected => connected.IsLand && connected.OccupyingTeamFast() == team);
    }    
    /// <summary>
    /// A faction may deploy onto a country it ALREADY occupies — "build that army again". The piece
    /// standing there is notionally returned to the pool and deployed again, so the board does not
    /// change but the deploy is a real, reactable DeployUnitChangeEvent (GameAPI.DeployUnitToCountry
    /// handles the mechanics). That is why neither of these tests <c>!HasUnit(faction)</c> any more.
    ///
    /// Everything else is unchanged: a non-home-space BUILD still needs an adjacent supplied unit, and
    /// a sea space still needs a harbour. Rebuilding in place is a target that becomes legal, not a way
    /// around the placement rules — a faction's own unit is not adjacent to itself, so an isolated unit
    /// outside its home space still cannot rebuild where it stands.
    /// </summary>
    public bool CanBuild(Faction faction) =>
            CanRecruit(faction) && //Can faction recruit here (i.e., it's empty or occupied by their own team) AND
            (!IsHomeSpace(faction) ? HasAdjacentSuppliedUnit(faction) : true) && //If it's not their home space, they must have an adjacent supplied unit AND
            (this.IsSea ? HasHarbor(faction) : true); //If it's a sea country, they must have a harbor (i.e., an adjacent land country occupied by their team)

    public bool CanRecruit(Faction faction)
    {
        // One read, not two: OccupyingTeam is not free, and this is on the CanBuild hot path.
        FactionTeam occupying = OccupyingTeamFast();
        return occupying == FactionTeam.NONE || occupying == StaticGameData.FactionTeamForFaction(faction);
    }
    public bool HasUnit(Faction faction) => Units.ContainsKey(faction);
    public bool IsHomeSpace(Faction faction) => FactionState.ForEnum(faction).FactionData.HomeSpaceCountryState.Id == this.Id;
    public List<int> AdjacentUnits(Faction faction) => AdjacentCountryStates(faction).Where(ccs => ccs.Units.ContainsKey(faction)).Select(ccs => ccs.Units[faction]).ToList();
    public List<int> AdjacentSuppliedUnits(Faction faction) => UnitState.ForIds(AdjacentUnits(faction)).Where(unit => unit.InSupply).Select(unit => unit.Id).ToList();

    /// <summary>
    /// Whether ANY adjacent unit of this faction is in supply — the question CanBuild actually asks.
    ///
    /// Short-circuits instead of going through <see cref="AdjacentSuppliedUnits"/>, which materialises
    /// three intermediate lists to answer a yes/no that usually resolves on the first neighbour. Same
    /// adjacency rules, same answer.
    /// </summary>
    public bool HasAdjacentSuppliedUnit(Faction faction) => AdjacentCountryIds(faction)
        .Any(id => ForId(id) is { } neighbour
                   && neighbour.Units.TryGetValue(faction, out int unitId)
                   && (UnitState.ForId(unitId)?.InSupply ?? false));
    public bool CanAttack(Faction faction) => HasAdjacentSuppliedUnit(faction) && HasAttackableUnit(faction);

    
    public List<BattleTarget> AdjacentBattleTargets(Faction attackingFaction, CountryType countryType)
    {
        List<BattleTarget> targets = new List<BattleTarget>();
        List<BattleTarget> targetableUnitIds = AdjacentCountryStates(attackingFaction)
            .Where(connectedCountryState => connectedCountryState.Type == countryType && connectedCountryState.CanAttack(attackingFaction)).ToList()
            .SelectMany(countryWithUnits => countryWithUnits.Units.Values.Where(unitId=>!UnitState.ForId(unitId).ImmuneForTurn)).Distinct().ToList()
            .Map(unitId => new BattleTarget(unitId, TargetType.UNIT));
        List<BattleTarget> targetableEmptyCountriesIds = AdjacentCountryStates(attackingFaction)
        .Where(connectedCountryState => connectedCountryState.Type == countryType && connectedCountryState.CanAttackWhenEmpty(attackingFaction)).ToList().ToCountryIds()
        .Map(countryId => new BattleTarget(countryId, TargetType.COUNTRY));
        targets.AddRange(targetableUnitIds);
        targets.AddRange(targetableEmptyCountriesIds);
        return targets;
    }
    public List<BattleTarget> BattleTargets(Faction attackingFaction)
    {
        if (!HasAdjacentSuppliedUnit(attackingFaction) || OccupyingTeam == StaticGameData.FactionTeamForFaction(attackingFaction))
        {
            return [];
        }
        else if (IsCountryEmpty)
        {
            return [new BattleTarget(Id, TargetType.COUNTRY)];
        }
        else
        {
            return Units.Values.ToList().Map(unitId => new BattleTarget(unitId, TargetType.UNIT));
        }
    }

    public bool HasAttackableUnit(Faction faction) => OccupyingTeam == StaticGameData.OpponentFactionTeamForFaction(faction);
    public bool CanAttackWhenEmpty(Faction faction) => HasAdjacentSuppliedUnit(faction) && IsCountryEmpty;

    /**
    * Static helpers
    */
    /// <summary>
    /// Every country. Returns the live list rather than copying it — the set of countries is fixed at
    /// setup, callers only ever read it, and the copy this replaced was allocated on every read from
    /// inside GameStateCalculator's per-faction loops.
    /// </summary>
    public static List<CountryState> AllCountryStates => GameSession.Current.GameState.CountryStates;
    public static CountryState ForId(int id) => GameSession.Current.GameState.CountryStateById.GetValueOrDefault(id) ?? null;

    public static List<CountryState> ForIds(IEnumerable<int> ids) => ids.Select(ForId).ToList();

    public static CountryState ForName(string name) => GameSession.Current.GameState.CountryStateByName.GetValueOrDefault(name) ?? null;
    public static List<CountryState> ForNames(List<string> names) => names.Map(n => GameSession.Current.GameState.CountryStateByName.GetValueOrDefault(n) ?? null);
    public static List<CountryState> ForUnitIds(List<int> unitIds) => ForIds(UnitState.ForIds(unitIds).Select(u => u.CountryId));

    public static CountryState ForEnum(Country country) => ForEnums(new List<Country> { country })[0];
    public static List<CountryState> ForEnums(List<Country> countries) => ForIds(countries.ToList().Map(countryEnum => (int)countryEnum));


    /**
    * Tag helpers
    */
    public static List<CountryState> WithTag(Tag tag, Faction faction) => AllCountryStates.Where(cs => cs.Tags.Has(tag, faction)).ToList();
    public static List<CountryState> WithTags(Tag[] tags, Faction faction) => AllCountryStates.Where(cs => tags.All(tag => cs.Tags.Has(tag, faction))).ToList();    
    
    public static List<CountryState> BuildableLand(Faction faction) => 
        WithTags(new[] { Tag.Buildable, Tag.LandCountry }, faction);
    
    public static List<CountryState> BuildableSea(Faction faction) => 
        WithTags(new[] { Tag.Buildable, Tag.SeaCountry }, faction);
    
    public static List<CountryState> RecruitableLand(Faction faction) => 
        WithTags(new[] { Tag.Recruitable, Tag.LandCountry }, faction);
    
    public static List<CountryState> RecruitableSea(Faction faction) => 
        WithTags(new[] { Tag.Recruitable, Tag.SeaCountry }, faction);
    
    public static List<CountryState> AttackableLand(Faction faction) => 
        WithTags(new[] { Tag.Attackable, Tag.LandCountry }, faction);
    
    public static List<CountryState> AttackableSea(Faction faction) => 
        WithTags(new[] { Tag.Attackable, Tag.SeaCountry }, faction);

    // ID helpers
    public static List<int> BuildableLandIds(Faction faction) => 
        BuildableLand(faction).Select(c => c.Id).ToList();
    
    public static List<int> BuildableSeaIds(Faction faction) => 
        BuildableSea(faction).Select(c => c.Id).ToList();
    
    public static List<int> RecruitableLandIds(Faction faction) => 
        RecruitableLand(faction).Select(c => c.Id).ToList();
    
    public static List<int> RecruitableSeaIds(Faction faction) => 
        RecruitableSea(faction).Select(c => c.Id).ToList();
    
    public static List<int> AttackableLandIds(Faction faction) => 
        AttackableLand(faction).Select(c => c.Id).ToList();
    
    public static List<int> AttackableSeaIds(Faction faction) => 
        AttackableSea(faction).Select(c => c.Id).ToList();
    
    public static bool operator ==(CountryState countryState, Country country)
    {
        return countryState.Id == (int)country;
    }
    public static bool operator !=(CountryState countryState, Country country) {
        return countryState.Id != (int)country;
    }

    public override bool Equals(object obj)
    {
        if (obj is Country countryEnum)
        {
            return Id == (int)countryEnum;
        }
        else if (obj is CountryState countryState)
        {
            return Id == countryState.Id;
        }
        else
        {
            return false;
        }
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id, Name);
    }
}
