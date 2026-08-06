using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Severity drives what the popup offers. See <see cref="ErrorReporter"/> for how it is classified.
/// </summary>
public enum ErrorSeverity
{
    /// <summary>Already recovered in place (e.g. a CardStep abandoned its own step). Popup is informational.</summary>
    Soft,

    /// <summary>Escaped to the turn loop but nothing was mutated. Popup offers Continue.</summary>
    Recoverable,

    /// <summary>Thrown mid-mutation before the change reached clients, or context was lost. No Continue.</summary>
    Unrecoverable
}

/// <summary>
/// One reported failure, in wire format. Serialized to a JSON string for RPC — Godot RPC marshals
/// Variants, so the object itself cannot cross; this follows the same convention as
/// <c>ChangeEventDto</c> and <c>InputRequest</c>.
/// </summary>
public class GameError
{
    public string Message { get; set; } = "";
    public string ExceptionType { get; set; } = "";
    public string StackTrace { get; set; } = "";
    public ErrorSeverity Severity { get; set; } = ErrorSeverity.Recoverable;

    // ── Where it was thrown ──────────────────────────────────────────────────
    /// <summary>Peer that threw. 1 = host. Not necessarily the peer this error is *about*.</summary>
    public int OriginPeerId { get; set; } = 0;

    /// <summary>Human label for the origin: SERVER / HOST / CLIENT n.</summary>
    public string OriginLabel { get; set; } = "";

    // ── Who the work was for ─────────────────────────────────────────────────
    /// <summary>
    /// Faction the failing operation was acting on behalf of, or null when there isn't one
    /// (menus, boot, queue plumbing). Distinct from <see cref="OriginPeerId"/>: the host drives
    /// steps for remote players, and one peer can control several factions.
    /// </summary>
    public Faction? TargetFaction { get; set; }

    /// <summary>Peer controlling <see cref="TargetFaction"/>, resolved via PlayerFactionRegistry. 0 when unknown.</summary>
    public int TargetPeerId { get; set; } = 0;

    /// <summary>Breadcrumb: turn / round / step / faction / card, plus the call-site context string.</summary>
    public string Context { get; set; } = "";

    public string Timestamp { get; set; } = "";

    /// <summary>Bumped instead of queueing a duplicate. Shown as ×N in the popup.</summary>
    [JsonIgnore] public int Occurrences { get; set; } = 1;

    /// <summary>Identity used for deduplication: type + message + first stack frame.</summary>
    [JsonIgnore]
    public string DedupeKey
    {
        get
        {
            string firstFrame = StackTrace?
                .Split('\n')
                .FirstOrDefault(line => line.TrimStart().StartsWith("at "))?
                .Trim() ?? "";
            return $"{ExceptionType}|{Message}|{firstFrame}";
        }
    }

    /// <summary>Single-line summary for the popup header.</summary>
    [JsonIgnore]
    public string OriginSummary
    {
        get
        {
            string target = TargetFaction.HasValue
                ? $" · for {TargetFaction.Value}" + (TargetPeerId > 0 ? $" (peer {TargetPeerId})" : "")
                : "";
            return $"{OriginLabel}{target}";
        }
    }

    /// <summary>Full plain-text form used by the Copy button and the console log.</summary>
    public string ToPlainText()
        => $"[{Timestamp}] {ExceptionType}: {Message}\n"
         + $"Origin: {OriginLabel} (peer {OriginPeerId})\n"
         + $"Target: {(TargetFaction.HasValue ? TargetFaction.Value.ToString() : "n/a")}"
         + $"{(TargetPeerId > 0 ? $" (peer {TargetPeerId})" : "")}\n"
         + $"Severity: {Severity}\n"
         + $"Context: {Context}\n"
         + $"{StackTrace}";

    public string ToJson() => JsonSerializer.Serialize(this);

    public static GameError FromJson(string json) => JsonSerializer.Deserialize<GameError>(json);
}
