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

    /// <summary>
    /// Optional. An arrow to hold on screen for as long as this message is up. Unset means no arrow,
    /// which is the normal case.
    /// </summary>
    [JsonPropertyName("arrow")] public TutorialArrowData Arrow { get; set; }

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

/// <summary>
/// Where a message's arrow points: a place on the screen and a direction, rather than a reference to
/// any particular node.
///
/// Deliberately not a node lookup. An arrow aimed at a named HUD element can only ever point at HUD
/// elements — and half of what a lesson wants to indicate is on the map, or is a gap between two
/// panels. A coordinate can point at all of it, and it needs no vocabulary to learn and no registry
/// to keep in step with the scene tree. The cost is that these are hand-tuned numbers: move a panel
/// and the arrows aimed at it go stale silently, because nothing here knows what it is pointing at.
///
/// The one POCO for this, rather than a parse-layer twin and a runtime one. It is read straight off
/// the tutorial file AND carried on the GameMessage wire — the two shapes never diverged, so the
/// copy between them was pure ceremony.
/// </summary>
public class TutorialArrowData
{
    /// <summary>Where the arrow's TIP lands across the viewport: 0 is the left edge, 1 the right.</summary>
    [JsonPropertyName("x")] public float X { get; set; }

    /// <summary>Where the arrow's TIP lands down the viewport: 0 is the top, 1 the bottom.</summary>
    [JsonPropertyName("y")] public float Y { get; set; }

    /// <summary>
    /// Which way the arrow points, in degrees, clockwise from pointing right — the direction the
    /// source texture already faces, so 0 needs no correction. 90 points down, 180 left, 270 up.
    /// </summary>
    [JsonPropertyName("rotation")] public float Rotation { get; set; }

    /// <summary>
    /// Throw unless this describes a point on screen. Called from TutorialStep.From, so a bad
    /// coordinate is a loud failure at load rather than a message whose arrow is simply never seen.
    ///
    /// The mistake worth catching is pixels typed into a viewport-relative field: <c>"x": 960</c> is
    /// not a visibly silly number, and without this it would put the arrow a long way off the right
    /// edge with nothing to say so. Out of range is therefore an error, not a clamp.
    /// </summary>
    /// <exception cref="System.Exception">A coordinate is outside the viewport.</exception>
    public void Validate()
    {
        if (X is < 0f or > 1f || Y is < 0f or > 1f)
            throw new System.Exception(
                $"Tutorial script has an arrow at ({X}, {Y}), which is off screen. " +
                "Arrow x and y are fractions of the viewport, between 0 and 1 — " +
                "0.5, 0.5 is the middle of the screen, not pixels.");
    }

    public override string ToString() => $"({X:0.###}, {Y:0.###}) at {Rotation:0}°";
}
