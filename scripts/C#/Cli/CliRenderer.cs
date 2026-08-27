using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

/// <summary>
/// One structured event, rendered either as readable text or as a single JSON object per line.
///
/// Everything the CLI emits goes through here, so switching modes is one flag rather than a second
/// set of call sites. Note the consumer rule for JSON mode: Godot prints its own banner before any
/// user code runs, so a parser must ignore stdout lines that are not valid JSON.
/// </summary>
public sealed class CliEvent
{
    public string Type;
    public readonly Dictionary<string, object> Fields = new();
    private string _text;

    public CliEvent(string type) { Type = type; }

    public CliEvent Set(string key, object value) { Fields[key] = value; return this; }

    /// <summary>The human-readable rendering. Falls back to the field dump when not set.</summary>
    public CliEvent Text(string text) { _text = text; return this; }

    public string ToText() => _text ?? $"{Type.ToUpperInvariant()} " +
        string.Join("  ", Fields.Select(kv => $"{kv.Key}={kv.Value}"));

    public string ToJson()
    {
        Dictionary<string, object> payload = new() { ["type"] = Type };
        foreach (var (key, value) in Fields) payload[key] = value;
        return JsonSerializer.Serialize(payload);
    }
}

/// <summary>Formats and writes <see cref="CliEvent"/>s to the transport.</summary>
public sealed class CliRenderer
{
    private readonly ICliTransport _transport;

    public bool JsonMode { get; set; }

    public CliRenderer(ICliTransport transport, bool jsonMode)
    {
        _transport = transport;
        JsonMode = jsonMode;
    }

    public void Emit(CliEvent e) => _transport.Write((JsonMode ? e.ToJson() : e.ToText()) + "\n");

    /// <summary>Plain output for inspection commands — a JSON string payload in JSON mode.</summary>
    public void Write(string text) => Emit(new CliEvent("output").Set("text", text).Text(text));

    public void Ok(string text) => Emit(new CliEvent("ok").Set("text", text).Text("OK  " + text));

    public void Error(string text) => Emit(new CliEvent("error").Set("text", text).Text("ERR " + text));

    /// <summary>
    /// Render an open prompt with its enumerated options. 1-based indices are the primary way to
    /// answer: unambiguous for a scripted caller, and it means nobody has to know that a SelectFaction
    /// answer is a faction enum value living in ResponseCardIds.
    /// </summary>
    public void Prompt(InputRequestSpec spec, InputRequest request)
    {
        CliEvent e = new CliEvent("prompt")
            .Set("kind", spec.Kind)
            .Set("faction", spec.Faction.ToString())
            .Set("title", spec.Title)
            .Set("min", spec.MinSelections)
            .Set("max", spec.MaxSelections)
            .Set("can_pass", spec.CanPass)
            .Set("options", spec.Options.Select((o, i) => new Dictionary<string, object>
            {
                ["index"] = i + 1,
                ["id"] = o.Id,
                ["kind"] = o.Kind.ToString().ToLowerInvariant(),
                ["label"] = o.Label,
            }).ToList());

        if (request.TriggerCardId > -1)
            e.Set("trigger_card", CardState.ForId(request.TriggerCardId)?.CardName ?? $"#{request.TriggerCardId}");
        if (!string.IsNullOrEmpty(request.TriggerSummaryText))
            e.Set("trigger", request.TriggerSummaryText);
        // Which window and what caused it — the two things the summary alone leaves out. Raw enum
        // name, like the other CLI views, because that is what .qgc scripts type.
        if (request.TriggerReactionKind != TriggerKind.NONE)
            e.Set("trigger_kind", request.TriggerReactionKind.ToString());
        if (!string.IsNullOrEmpty(request.TriggerCauseText))
            e.Set("trigger_cause", request.TriggerCauseText);
        if (!string.IsNullOrEmpty(request.TriggerBulletinLabel))
            e.Set("bulletin", request.TriggerBulletinLabel);

        StringBuilder sb = new();
        sb.Append($"PROMPT {spec.Kind}  faction={spec.Faction}  {spec.Title}");
        if (request.TriggerCardId > -1)
            sb.Append($"\n  trigger: {CardState.ForId(request.TriggerCardId)?.CardName}"
                      + (request.TriggerReactionKind != TriggerKind.NONE ? $" ({request.TriggerReactionKind})" : ""));
        if (!string.IsNullOrEmpty(request.TriggerCauseText))
            sb.Append($"\n  cause: {request.TriggerCauseText}");
        if (!string.IsNullOrEmpty(request.TriggerBulletinLabel))
            sb.Append($"\n  bulletin: {request.TriggerBulletinLabel}");

        if (spec.Options.Count == 0) sb.Append("\n  (no options)");
        for (int i = 0; i < spec.Options.Count; i++)
            sb.Append($"\n  {i + 1,3}. [{spec.Options[i].Id}] {spec.Options[i].Label}");

        // The cards the GUI draws greyed out beside the choosable ones: a reaction window shows the
        // faction's whole event-triggered table so the player can see why nothing of theirs applies.
        // Not answerable, so deliberately unnumbered — `answer` matches against spec.Options only.
        List<int> shownOnly = (request.DisplayCardIds ?? new List<int>())
            .Except(spec.Options.Select(o => o.Id)).ToList();
        if (shownOnly.Count > 0)
        {
            e.Set("shown_not_selectable", shownOnly);
            sb.Append("\n  shown, not selectable: "
                      + string.Join(", ", shownOnly.Select(id => CardState.ForId(id)?.CardName ?? $"card#{id}")));
        }

        if (spec.MinSelections == spec.MaxSelections)
            sb.Append($"\n  choose exactly {spec.MinSelections}");
        else if (spec.MinSelections == 0)
            sb.Append($"\n  choose up to {spec.MaxSelections}");
        else
            sb.Append($"\n  choose {spec.MinSelections}-{spec.MaxSelections}");
        if (spec.CanPass) sb.Append(", or `pass`");

        Emit(e.Text(sb.ToString()));
    }
}
