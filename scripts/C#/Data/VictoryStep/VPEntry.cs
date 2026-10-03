using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

public partial class VPEntry
{
    public int VictoryPoints { get; set; }

    /// <summary>
    /// The cause alone, with no point count in it — the count is rendered by <see cref="Description"/>.
    /// Reads as the tail of "+3 VP — ...", e.g. "supply star on Italy".
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Display form shared by the VP breakdown panel and the victory-step notification. Derived, so
    /// it stays off the wire — otherwise every replicated VPEntry ships a copy of its own Reason.
    /// </summary>
    [JsonIgnore] public string Description => $"{Sign}{VictoryPoints} VP — {Reason}";

    /// <summary>Negative values already carry their own '-'; zero gets a space so the column lines up.</summary>
    private string Sign => VictoryPoints > 0 ? "+" : VictoryPoints < 0 ? "" : " ";

    /// <summary>
    /// Where these points came from on the map, as country id to the VP floated over it. One entry can
    /// span several countries, so a card's score stays one line in the breakdown. Null for board-wide scores.
    /// </summary>
    public Dictionary<int, int> SourceCountryVPs { get; set; }

    /// <remarks>
    /// Keep this the only constructor: System.Text.Json binds these parameters by name to round-trip
    /// through ScorePointsChangeEventDto, so each must keep its property's name and type.
    /// </remarks>
    public VPEntry(int victoryPoints, string reason, Dictionary<int, int> sourceCountryVPs = null)
    {
        this.VictoryPoints = victoryPoints;
        this.Reason = reason;
        this.SourceCountryVPs = sourceCountryVPs;
    }

    /// <summary>All the points shown on one country.</summary>
    public static VPEntry ForCountry(int victoryPoints, string reason, int countryId)
        => new VPEntry(victoryPoints, reason, new Dictionary<int, int> { [countryId] = victoryPoints });

    /// <summary><paramref name="vpEach"/> per unit, each shown on the country it stands in.</summary>
    public static VPEntry ForUnits(IEnumerable<UnitState> units, BoardState board, int vpEach, string reason)
    {
        Dictionary<int, int> byCountry = units
            .GroupBy(board.CountryOf)
            .ToDictionary(g => g.Key, g => g.Count() * vpEach);
        return new VPEntry(byCountry.Values.Sum(), reason, byCountry);
    }

    /// <summary><paramref name="vpEach"/> per distinct country, for cards that pay per occupied space rather than per unit.</summary>
    public static VPEntry ForCountries(IEnumerable<int> countryIds, int vpEach, string reason)
    {
        Dictionary<int, int> byCountry = countryIds.Distinct().ToDictionary(id => id, _ => vpEach);
        return new VPEntry(byCountry.Values.Sum(), reason, byCountry);
    }
}
