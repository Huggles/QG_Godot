using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// Wire format for a tutorial script: <c>assets/data/tutorials/*.json</c>. Plain POCOs deserialized
/// with System.Text.Json, matching InitialGameStateData's conventions exactly.
///
/// A tutorial is a scenario plus one of these. The scenario already expresses the whole opening
/// position — deployments, fixed hands, discards, victory points, round limit, opening discard — so
/// this file only carries what happens AFTER the deal.
/// </summary>
public class TutorialScriptData
{
    [JsonPropertyName("id")]    public string Id { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; }

    /// <summary>
    /// The faction the human plays. Every prompt for it goes to the GUI; every prompt for the other
    /// five is answered from <see cref="Steps"/>, or passed.
    /// </summary>
    [JsonPropertyName("learnerFaction")] public string LearnerFaction { get; set; } = "GERMANY";

    /// <summary>
    /// What to do with a prompt the script does not claim: <c>lenient</c> hands it to the player
    /// (a half-written script stays playable), <c>strict</c> reports it through ErrorReporter, and
    /// <c>pass</c> declines it. See TutorialInputProvider for why lenient is the default.
    /// </summary>
    [JsonPropertyName("onUnexpectedPrompt")] public string OnUnexpectedPrompt { get; set; } = "lenient";

    [JsonPropertyName("steps")] public List<TutorialStepData> Steps { get; set; } = new();
}

/// <summary>
/// One instruction. A flat shape rather than [JsonPolymorphic]: the vocabulary is small, the fields
/// are mostly disjoint, and a flat DTO keeps hand-authoring a tutorial to plain JSON with no $type
/// discriminator to remember. Validated per-type when the script is loaded.
///
/// Two anchoring axes, and which one applies is decided by <see cref="Type"/>:
///   at-anchored    (message, highlight, clearHighlight, end) fire from the GameFlow hooks
///   prompt-anchored (answer, constrain)                      are consumed by the input seam
/// </summary>
public class TutorialStepData
{
    /// <summary>message | constrain | answer | highlight | clearHighlight | end</summary>
    [JsonPropertyName("type")] public string Type { get; set; }

    // ── at-anchored ──────────────────────────────────────────────────────────
    [JsonPropertyName("at")]   public TutorialAnchorData At { get; set; }

    /// <summary>BBCode: the panel's label is a RichTextLabel with bbcode enabled.</summary>
    [JsonPropertyName("text")] public string Text { get; set; }

    /// <summary>False shows the message and carries straight on, without waiting for CONTINUE.</summary>
    [JsonPropertyName("wait")] public bool Wait { get; set; } = true;

    // ── prompt-anchored ──────────────────────────────────────────────────────
    /// <summary>
    /// Exactly <c>InputRequestSpec.Kind</c> — the request class name with "RequestHandler" stripped:
    /// HandCardPlay, ActivateCard, BlockReaction, SelectCountry, SelectUnit, SelectBattleTarget,
    /// SelectCard, SelectCards, SelectFaction, SelectOption, ReorderCards, HandCardsDiscard,
    /// ForceDiscardHandCards. "*" matches any kind.
    /// </summary>
    [JsonPropertyName("prompt")]  public string Prompt { get; set; } = "*";

    /// <summary>Which faction's prompt this claims. "*" matches any.</summary>
    [JsonPropertyName("faction")] public string Faction { get; set; } = "*";

    /// <summary>Decline, using the request's own pass idiom (see PassMode).</summary>
    [JsonPropertyName("pass")]      public bool Pass { get; set; }

    // Selectors, matched against the prompt's OWN option list — never against the whole game.
    // Cards match on Label first, then UniqueName, so a script may use either.
    [JsonPropertyName("card")]      public string Card { get; set; }
    [JsonPropertyName("cards")]     public List<string> Cards { get; set; } = new();
    [JsonPropertyName("country")]   public string Country { get; set; }
    [JsonPropertyName("countries")] public List<string> Countries { get; set; } = new();
    [JsonPropertyName("option")]    public string Option { get; set; }

    /// <summary>Authoring comment. Read by nobody; exists because JSON has no comments.</summary>
    [JsonPropertyName("note")] public string Note { get; set; }
}

/// <summary>Where an at-anchored instruction fires. Unset fields match anything.</summary>
public class TutorialAnchorData
{
    [JsonPropertyName("turn")]    public int? Turn { get; set; }
    [JsonPropertyName("round")]   public int? Round { get; set; }
    [JsonPropertyName("faction")] public string Faction { get; set; }

    /// <summary>A TurnStep enum name. Null anchors to the turn rather than to a step.</summary>
    [JsonPropertyName("step")]    public string Step { get; set; }

    /// <summary>BEFORE (the step has opened) | AFTER (the step has fully resolved).</summary>
    [JsonPropertyName("timing")]  public string Timing { get; set; } = "BEFORE";
}
