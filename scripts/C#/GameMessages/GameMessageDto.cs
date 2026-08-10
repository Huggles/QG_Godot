using System.Text.Json.Serialization;

/// <summary>
/// Wire format for the replicated game-message stream. Only constructor parameters are included —
/// never GodotObject references or derived state; live objects travel as ids and are rehydrated by
/// lookup on the receiving peer.
///
/// This is the ONE polymorphic root for the whole hierarchy. Every message DTO, whichever branch it
/// belongs to, registers its discriminator here. Do not add [JsonPolymorphic] to ChangeEventDto or
/// PresentationEventDto as well: System.Text.Json resolves polymorphism from the type it is handed, and
/// two configurations covering the same leaf types is a silent-divergence footgun.
///
/// Adding a message means three things: a [JsonDerivedType] line below, an arm in
/// <see cref="GameMessage.FromDto"/>, and — the decision the old single hierarchy let you skip —
/// choosing which branch it belongs to.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
// ── State mutations (ChangeEvent) ────────────────────────────────────────────
[JsonDerivedType(typeof(DeployUnitChangeEventDto),       "DeployUnit")]
[JsonDerivedType(typeof(BattleCountryChangeEventDto),    "BattleCountry")]
[JsonDerivedType(typeof(RemoveUnitChangeEventDto),       "RemoveUnit")]
[JsonDerivedType(typeof(BattleUnitChangeEventDto),       "BattleUnit")]
[JsonDerivedType(typeof(PlayCardChangeEventDto),         "PlayCard")]
[JsonDerivedType(typeof(ActivateReactionChangeEventDto), "ActivateReaction")]
[JsonDerivedType(typeof(ForceDiscardCardsChangeEventDto),     "DiscardCards")]
[JsonDerivedType(typeof(DrawCardsChangeEventDto),        "DrawCards")]
[JsonDerivedType(typeof(ScorePointsChangeEventDto),      "ScorePoints")]
[JsonDerivedType(typeof(SetStartingScoreChangeEventDto), "SetStartingScore")]
[JsonDerivedType(typeof(DiscardHandCardsChangeEventDto), "VoluntaryDiscardCards")]
[JsonDerivedType(typeof(ForceDiscardHandCardsChangeEventDto), "ForceDiscardHandCards")]
[JsonDerivedType(typeof(DrawCardByNameChangeEventDto),   "DrawCardByName")]
[JsonDerivedType(typeof(ChangeStepChangeEventDto),       "ChangeStep")]
[JsonDerivedType(typeof(ChangeRoundChangeEventDto),      "ChangeRound")]
[JsonDerivedType(typeof(RecycleCardChangeEventDto),      "RecycleCard")]
[JsonDerivedType(typeof(SpendPlayActionChangeEventDto),  "SpendPlayAction")]
[JsonDerivedType(typeof(ReorderDeckChangeEventDto),      "ReorderDeck")]
[JsonDerivedType(typeof(GrantSupplyChangeEventDto),       "GrantSupply")]
[JsonDerivedType(typeof(RegisterBulletinCardChangeEventDto), "RegisterBulletinCard")]
// ── Derived-state delivery ───────────────────────────────────────────────────
[JsonDerivedType(typeof(RecalculateTagsMessageDto),      "RecalculateTags")]
// ── Presentation only (PresentationEvent) ────────────────────────────────────
[JsonDerivedType(typeof(ShowBulletinPresentationEventDto),    "ShowBulletin")]
[JsonDerivedType(typeof(ShowActionLabelPresentationEventDto), "ShowActionLabel")]
public abstract class GameMessageDto
{
    public int     Id                  { get; set; }
    public Faction TriggeringFaction   { get; set; }
    public Faction TargetFaction       { get; set; }
    public bool    PlayAnimations      { get; set; } = true;
    public bool    BlockAnimationQueue { get; set; } = true;

    /// <summary>
    /// Fill the fields every message on the channel carries. Branch-specific Build helpers call this
    /// first, then add their own — see <see cref="ChangeEventDto.Build{T}"/>.
    /// </summary>
    protected static void Fill(GameMessageDto dto, GameMessage message, int id)
    {
        dto.Id                  = id;
        dto.TriggeringFaction   = message.TriggeringFaction;
        dto.TargetFaction       = message.TargetFaction;
        dto.PlayAnimations      = message.PlayAnimations;
        dto.BlockAnimationQueue = message.BlockAnimationQueue;
    }
}

/// <summary>Wire format for a <see cref="PresentationEvent"/>: no hash, no reaction flags.</summary>
public abstract class PresentationEventDto : GameMessageDto
{
    public static T Build<T>(PresentationEvent presentationEvent, int Id) where T : PresentationEventDto, new()
    {
        T dto = new T();
        Fill(dto, presentationEvent, Id);
        return dto;
    }
}

public class ShowBulletinPresentationEventDto : PresentationEventDto
{
    public string Label        { get; set; }
    public string BulletinText { get; set; }
}

public class ShowActionLabelPresentationEventDto : PresentationEventDto
{
    public string Text { get; set; }
}

/// <summary>
/// Wire format for <see cref="RecalculateTagsMessage"/>. A direct GameMessageDto — it is neither a
/// state mutation nor presentation.
/// </summary>
public class RecalculateTagsMessageDto : GameMessageDto
{
    public ComputedTagsSnapshot Snapshot { get; set; }

    public static RecalculateTagsMessageDto Build(RecalculateTagsMessage message, int Id)
    {
        RecalculateTagsMessageDto dto = new RecalculateTagsMessageDto();
        Fill(dto, message, Id);
        return dto;
    }
}
