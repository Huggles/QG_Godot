using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;

/// <summary>
/// Authoritative game state for multiplayer. Owned by MultiplayerSession.
/// Holds live GodotObject instances for game logic, and provides
/// BuildSnapshot / ApplySnapshot / ComputeHash for wire-format synchronization.
/// Replaces the old hotseat-era GameState.
/// </summary>
public class MultiplayerGameState
{
    // -------------------------------------------------------------------------
    // Live state — GodotObject instances, NOT serialized directly
    // -------------------------------------------------------------------------

    public List<CountryState>              CountryStates  { get; set; } = new();
    public List<UnitState>                 UnitStates     { get; set; } = new();
    public List<CardState>                 CardStates     { get; set; } = new();
    public List<StraightState>             StraightStates { get; set; } = new();
    public List<FactionState>              FactionStates  { get; set; } = new();
    
    public List<ChangeEvent>               GameChangeEvents { get; set; } = new();
    public List<CardStep>                  CardSteps    { get; set; } = new();

    // -------------------------------------------------------------------------
    // Lookup caches — lazily populated from the lists above
    // -------------------------------------------------------------------------

    [JsonIgnore] public Dictionary<int, CountryState> CountryStateById => CountryStates.ToDictionary(cs => cs.Id);

    [JsonIgnore] public Dictionary<string, CountryState> CountryStateByName => CountryStates.ToDictionary(cs => cs.Name); // no lazy population needed since this is just a different view of the same data as CountryStateById
   
    [JsonIgnore] public Dictionary<int, UnitState> UnitStatesById => UnitStates.ToDictionary(us => us.Id);

    [JsonIgnore] public Dictionary<int, CardState> CardStatesById => CardStates.ToDictionary(cs => cs.Id);

    [JsonIgnore] public Dictionary<string, CardState> CardStatesByName => CardStates.ToDictionary(cs => cs.CardData.UniqueName + cs.Id);

    [JsonIgnore] public Dictionary<int, StraightState> StraightStateById => StraightStates.ToDictionary(ss => ss.Id);

    [JsonIgnore] public Dictionary<int, StraightState> StraightStateByControllingCountryId => StraightStates.ToDictionary(ss => ss.ControllingCountryId);

    [JsonIgnore] public Dictionary<Faction, FactionState> FactionStatesByFaction => FactionStates.ToDictionary(fs => fs.Faction);

    [JsonIgnore] public Dictionary<Faction, FactionState> PlayableFactionStatesByFaction => FactionStates.Where(fs => fs.Playable).ToDictionary(fs => fs.Faction);

    [JsonIgnore] public Dictionary<int, CardStep> CardStepsById => CardSteps.ToDictionary(cs => cs.Id);
    
    [JsonIgnore] public List<FactionState> PlayableFactionStates => FactionStates.Where(fs => fs.Playable).ToList();



    // -------------------------------------------------------------------------
    // Serialization — Pattern B / Pattern A sync
    // -------------------------------------------------------------------------

    public MultiplayerGameStateSnapshot BuildSnapshot()
    {
        return new MultiplayerGameStateSnapshot
        {
            CountryStates = CountryStates.Select(cs => new CountryStateDto
            {
                Id    = cs.Id,
                Units = new Dictionary<Faction, int>(cs.Units)
            }).ToList(),

            UnitStates = UnitStates.Select(us => new UnitStateDto
            {
                Id              = us.Id,
                CountryId       = us.CountryId,
                ImmuneForTurn   = us.ImmuneForTurn,
                SuppliedForTurn = us.SuppliedForTurn
            }).ToList(),

            StraightStates = StraightStates.Select(ss => new StraightStateDto
            {
                Id                   = ss.Id,
                ControllingCountryId = ss.ControllingCountryId
            }).ToList(),

            FactionStates = FactionStates.Select(fs => new FactionStateDto
            {
                Faction = fs.Faction,
                Score   = fs.Score,
                Deck    = new DeckStateDto
                {
                    DeckCardIds      = new List<int>(fs.DeckState.DeckCardIds),
                    HandCardIds      = new List<int>(fs.DeckState.HandCardIds),
                    DiscardedCardIds = new List<int>(fs.DeckState.DiscardedCardIds),
                    ResponseCardIds  = new List<int>(fs.DeckState.ResponseCardIds),
                    StatusCardIds    = new List<int>(fs.DeckState.StatusCardIds)
                }
            }).ToList()
        };
    }

    public void ApplySnapshot(MultiplayerGameStateSnapshot snapshot)
    {
        foreach (var dto in snapshot.CountryStates)
        {
            var cs   = CountryStateById[dto.Id];
            cs.Units = dto.Units;
        }

        foreach (var dto in snapshot.UnitStates)
        {
            var us              = UnitStatesById[dto.Id];
            us.CountryId         = dto.CountryId;
            us.ImmuneForTurn     = dto.ImmuneForTurn;
            us.SuppliedForTurn   = dto.SuppliedForTurn;
        }

        foreach (var dto in snapshot.StraightStates)
        {
            var ss                   = StraightStateById[dto.Id];
            ss.ControllingCountryId  = dto.ControllingCountryId;
        }

        foreach (var dto in snapshot.FactionStates)
        {
            var fs    = FactionStatesByFaction[dto.Faction];
            fs.Score  = dto.Score;
            var deck  = fs.DeckState;
            deck.DeckCardIds.Clear();      deck.DeckCardIds.AddRange(dto.Deck.DeckCardIds);
            deck.HandCardIds.Clear();      deck.HandCardIds.AddRange(dto.Deck.HandCardIds);
            deck.DiscardedCardIds.Clear(); deck.DiscardedCardIds.AddRange(dto.Deck.DiscardedCardIds);
            deck.ResponseCardIds.Clear();  deck.ResponseCardIds.AddRange(dto.Deck.ResponseCardIds);
            deck.StatusCardIds.Clear();    deck.StatusCardIds.AddRange(dto.Deck.StatusCardIds);
        }

        // Tags are never in the snapshot — always recomputed
        GameStateCalculator.CalculateAll();
    }

    public string ComputeHash()
    {
        var sb = new StringBuilder();

        foreach (var cs in CountryStates.OrderBy(c => c.Id))
            sb.Append($"C{cs.Id}:{string.Join(",", cs.Units.OrderBy(kv => (int)kv.Key).Select(kv => $"{(int)kv.Key}={kv.Value}"))}|");

        foreach (var us in UnitStates.OrderBy(u => u.Id))
            sb.Append($"U{us.Id}:{us.CountryId},{us.ImmuneForTurn},{us.SuppliedForTurn}|");

        foreach (var ss in StraightStates.OrderBy(s => s.Id))
            sb.Append($"S{ss.Id}:{ss.ControllingCountryId}|");

        foreach (var kv in PlayableFactionStatesByFaction.OrderBy(kv => (int)kv.Key))
        {
            var deck = kv.Value.DeckState;
            sb.Append($"F{(int)kv.Key}:{kv.Value.Score}," +
                      $"d{deck.DeckCardIds.Count}," +
                      $"h{string.Join("-", deck.HandCardIds.OrderBy(id => id))}," +
                      $"st{string.Join("-", deck.StatusCardIds.OrderBy(id => id))}," +
                      $"r{string.Join("-", deck.ResponseCardIds.OrderBy(id => id))}|");
        }
        return Fnv1a32(sb.ToString()).ToString("X8");
    }

    private static uint Fnv1a32(string s)
    {
        uint hash = 2166136261u;
        foreach (char c in s) { hash ^= c; hash *= 16777619u; }
        return hash;
    }
}

// =============================================================================
// Snapshot — wire format (serialized / deserialized by JsonSerializer)
// =============================================================================

public class MultiplayerGameStateSnapshot
{
    public List<CountryStateDto>  CountryStates  { get; set; } = new();
    public List<UnitStateDto>     UnitStates     { get; set; } = new();
    public List<StraightStateDto> StraightStates { get; set; } = new();
    public List<FactionStateDto>  FactionStates  { get; set; } = new();
}

public class CountryStateDto
{
    public int Id { get; set; }
    public Dictionary<Faction, int> Units { get; set; } = new();
}

public class UnitStateDto
{
    public int  Id              { get; set; }
    public int  CountryId       { get; set; }
    public bool ImmuneForTurn   { get; set; }
    public bool SuppliedForTurn { get; set; }
}

public class StraightStateDto
{
    public int Id                   { get; set; }
    public int ControllingCountryId { get; set; }
}

public class FactionStateDto
{
    public Faction      Faction { get; set; }
    public int          Score   { get; set; }
    public DeckStateDto Deck    { get; set; } = new();
}

public class DeckStateDto
{
    public List<int> DeckCardIds      { get; set; } = new();
    public List<int> HandCardIds      { get; set; } = new();
    public List<int> DiscardedCardIds { get; set; } = new();
    public List<int> ResponseCardIds  { get; set; } = new();
    public List<int> StatusCardIds    { get; set; } = new();
}

