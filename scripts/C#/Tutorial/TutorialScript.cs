using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

/// <summary>What a <see cref="TutorialStep"/> does.</summary>
public enum TutorialStepType { Message, Constrain, Answer, Highlight, ClearHighlight, End }

/// <summary>What to do with a prompt the script does not claim.</summary>
public enum UnexpectedPromptPolicy { Lenient, Strict, Pass }

/// <summary>
/// A tutorial script, parsed and validated. Everything that can be wrong with the file — an unknown
/// instruction type, a faction or step name that does not exist — throws here, at load, naming the
/// offender. The same rule GameModeMultiplayerDefault.RegisterMutators applies to scenario mutators:
/// a typo must be a loud failure at startup, never a quietly-skipped instruction mid-lesson.
/// </summary>
public sealed class TutorialScript
{
    public string Id { get; private set; }
    public string Title { get; private set; }
    public Faction LearnerFaction { get; private set; }
    public UnexpectedPromptPolicy OnUnexpectedPrompt { get; private set; }
    public IReadOnlyList<TutorialStep> Steps { get; private set; }

    public static TutorialScript Load(string path)
    {
        using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null)
            throw new Exception($"Tutorial script could not be opened: {path} ({FileAccess.GetOpenError()})");

        // GetAsText() rather than the raw bytes, for the reason ReadScenario documents: it has
        // already stripped any BOM, which Deserialize would otherwise choke on.
        TutorialScriptData data = JsonSerializer.Deserialize<TutorialScriptData>(file.GetAsText());
        if (data == null) throw new Exception($"Tutorial script is empty or malformed: {path}");

        return new TutorialScript
        {
            Id                 = data.Id ?? path,
            Title              = data.Title ?? "Tutorial",
            LearnerFaction     = ParseFaction(data.LearnerFaction, "learnerFaction"),
            OnUnexpectedPrompt = ParsePolicy(data.OnUnexpectedPrompt),
            Steps              = data.Steps.Select(TutorialStep.From).ToList()
        };
    }

    internal static Faction ParseFaction(string name, string what)
    {
        if (string.IsNullOrWhiteSpace(name) || name == "*") return Faction.NONE;
        if (!Enum.TryParse(name, out Faction faction))
            throw new Exception($"Tutorial script names unknown faction '{name}' in {what}.");
        return faction;
    }

    private static UnexpectedPromptPolicy ParsePolicy(string name) => name?.ToLowerInvariant() switch
    {
        null or "" or "lenient" => UnexpectedPromptPolicy.Lenient,
        "strict"                => UnexpectedPromptPolicy.Strict,
        "pass"                  => UnexpectedPromptPolicy.Pass,
        _ => throw new Exception(
            $"Tutorial script has unknown onUnexpectedPrompt '{name}'. Expected lenient, strict or pass.")
    };
}

/// <summary>One parsed instruction. See <see cref="TutorialStepData"/> for the JSON shape.</summary>
public sealed class TutorialStep
{
    public TutorialStepType Type { get; private set; }

    // at-anchored
    /// <summary>
    /// Whether the instruction carried an "at" block at all. An instruction with none is unanchored:
    /// it runs at whatever point the cursor has already reached, immediately after whatever preceded
    /// it. That is the authoring expectation for a trailing "…and then end", and it is distinct from
    /// an "at" block whose fields happen to be unset — that one still anchors to a turn boundary.
    /// </summary>
    public bool HasAnchor { get; private set; }

    public int? Turn { get; private set; }
    public int? Round { get; private set; }
    public Faction AnchorFaction { get; private set; } = Faction.NONE;
    public TurnStep? Step { get; private set; }
    public MutatorTiming Timing { get; private set; } = MutatorTiming.BEFORE;
    public string Text { get; private set; }
    public bool Wait { get; private set; } = true;

    // prompt-anchored
    public string Prompt { get; private set; } = "*";
    public Faction PromptFaction { get; private set; } = Faction.NONE;
    public bool Pass { get; private set; }
    public List<string> Cards { get; private set; } = new();
    public List<string> Countries { get; private set; } = new();
    public string Option { get; private set; }

    /// <summary>True for the two types the input seam consumes rather than the GameFlow hooks.</summary>
    public bool IsPromptAnchored => Type is TutorialStepType.Constrain or TutorialStepType.Answer;

    public static TutorialStep From(TutorialStepData data)
    {
        TutorialStep step = new()
        {
            Type          = ParseType(data.Type),
            Text          = data.Text,
            Wait          = data.Wait,
            Prompt        = string.IsNullOrWhiteSpace(data.Prompt) ? "*" : data.Prompt,
            PromptFaction = TutorialScript.ParseFaction(data.Faction, "a step's faction"),
            Pass          = data.Pass,
            Option        = data.Option,
        };

        // One selector list per kind, so a script may write either the singular or the plural form.
        if (!string.IsNullOrWhiteSpace(data.Card)) step.Cards.Add(data.Card);
        step.Cards.AddRange(data.Cards);
        if (!string.IsNullOrWhiteSpace(data.Country)) step.Countries.Add(data.Country);
        step.Countries.AddRange(data.Countries);

        step.HasAnchor = data.At != null;
        if (data.At != null)
        {
            step.Turn          = data.At.Turn;
            step.Round         = data.At.Round;
            step.AnchorFaction = TutorialScript.ParseFaction(data.At.Faction, "an anchor's faction");
            step.Timing        = ParseTiming(data.At.Timing);

            if (!string.IsNullOrWhiteSpace(data.At.Step))
            {
                if (!Enum.TryParse(data.At.Step, out TurnStep turnStep))
                    throw new Exception($"Tutorial script names unknown step '{data.At.Step}'.");
                step.Step = turnStep;
            }
        }

        if (step.Type is TutorialStepType.Message && string.IsNullOrEmpty(step.Text))
            throw new Exception("Tutorial script has a message step with no text.");

        return step;
    }

    private static TutorialStepType ParseType(string name) => name?.ToLowerInvariant() switch
    {
        "message"        => TutorialStepType.Message,
        "constrain"      => TutorialStepType.Constrain,
        "answer"         => TutorialStepType.Answer,
        "highlight"      => TutorialStepType.Highlight,
        "clearhighlight" => TutorialStepType.ClearHighlight,
        "end"            => TutorialStepType.End,
        _ => throw new Exception(
            $"Tutorial script has unknown step type '{name}'. Expected message, constrain, answer, " +
            "highlight, clearHighlight or end.")
    };

    private static MutatorTiming ParseTiming(string name) => name?.ToUpperInvariant() switch
    {
        null or "" or "BEFORE" => MutatorTiming.BEFORE,
        "AFTER"                => MutatorTiming.AFTER,
        _ => throw new Exception($"Tutorial script has unknown timing '{name}'. Expected BEFORE or AFTER.")
    };

    /// <summary>
    /// Whether this at-anchored step fires at the given position.
    ///
    /// No "at" block at all means "here, now" — it runs wherever the cursor has already reached, which
    /// is what a trailing "…and then end" has to do. Within an "at" block, unset fields match anything,
    /// but an unset step still pins the instruction to a turn boundary rather than to any step.
    /// </summary>
    public bool MatchesAnchor(int turn, int round, Faction faction, TurnStep? step, MutatorTiming timing)
        => !HasAnchor
        || ((Turn == null || Turn == turn)
            && (Round == null || Round == round)
            && (AnchorFaction == Faction.NONE || AnchorFaction == faction)
            && (Step == null ? step == null : Step == step)
            && (step == null || Timing == timing));

    public override string ToString()
        => IsPromptAnchored
            ? $"{Type} {Prompt}/{PromptFaction}"
            : $"{Type} at turn={Turn?.ToString() ?? "*"} step={Step?.ToString() ?? "*"} {Timing}";
}
