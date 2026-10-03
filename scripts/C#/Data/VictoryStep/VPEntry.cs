using Godot;
using System;
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

    /// <summary>The country these points came from, which floats them over the map. Null for board-wide scores.</summary>
    public int? SourceCountryId { get; set; }

    /// <remarks>
    /// System.Text.Json binds these parameters by name to round-trip through ScorePointsChangeEventDto,
    /// so each must keep its property's name and type.
    /// </remarks>
    public VPEntry(int victoryPoints, string reason, int? sourceCountryId = null)
    {
        this.VictoryPoints = victoryPoints;
        this.Reason = reason;
        this.SourceCountryId = sourceCountryId;
    }
}
