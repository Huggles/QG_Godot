using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>
/// One entry in the replicated, ordered game-message stream: the server applies a message locally,
/// broadcasts it, and every client replays it from the same serial <see cref="ChangeEventQueue"/>.
///
/// Ordering is the whole reason this type exists rather than a bare Rpc. A raw Rpc body runs
/// immediately on a client while replicated messages sit deferred in the queue, so anything an Rpc
/// does can land before or after the effects it belongs with.
///
/// Three shapes ride the channel, and they are siblings rather than one type with a "does this
/// actually change anything?" flag:
///   • <see cref="ChangeEvent"/>          — mutates authoritative state. Hashed, registered in the
///                                          card play pool, recalculates tags, tracked for divergence.
///   • <see cref="PresentationEvent"/>    — mutates nothing. Its whole effect is its animations, so it
///                                          is never hashed and can never steer the reaction chain.
///   • <see cref="RecalculateTagsMessage"/> — delivers server-computed derived state; neither of the above.
///
/// What lives here is exactly what the channel itself needs: a stream position (<see cref="Id"/>), the
/// wire format, and the animation plumbing. Everything about mutation stays in ChangeEvent.
/// </summary>
public abstract partial class GameMessage : GodotObject
{
    private static int messageCounter = 0;

    public string ScriptName => GetType().ToString();

    /// <summary>
    /// Position in the replicated stream, assigned by the originating peer and carried on the wire.
    /// One counter across every message kind on purpose: ids must be unique channel-wide, because
    /// GameHistoryList de-duplicates on them and ChangeEvent.ForId resolves reaction sources by them.
    ///
    /// The counter also advances on a client (every message is constructed there too), so client-local
    /// values drift ahead of the server's. Harmless, because FromDto overwrites Id from the wire and a
    /// client never originates a message.
    /// </summary>
    public int Id { get; set; } = -1;

    public Faction TriggeringFaction { get; set; }
    public Faction TargetFaction { get; set; }

    public bool PlayAnimations { get; set; } = true;
    /// <summary>
    /// Forwarded by a message to its animations' own <see cref="ChangeEventAnimation.BlockQueue"/>.
    /// Note Apply() always awaits the animation queue draining regardless — see DeployUnitChangeEvent,
    /// the only message that actually reads this.
    /// </summary>
    public bool BlockAnimationQueue { get; set; } = true;

    /// <summary>Whether this message becomes a row in the game history UI.</summary>
    public virtual bool ToHistoryItem => true;

    protected GameMessage(Faction triggeringFaction)
    {
        TriggeringFaction = triggeringFaction;
        messageCounter += 1;
        Id = messageCounter;
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    /// <summary>
    /// Run this message's local effect. The single entry point for both the server (apply, then
    /// broadcast) and a client (replay something the server already applied). The loop epoch is
    /// captured here and handed down so a branch can bail before touching anything if error recovery
    /// moved the loop on underneath it.
    /// </summary>
    public Task<bool> Apply() => ApplyInternal(ErrorReporter.GameLoopEpoch);

    protected abstract Task<bool> ApplyInternal(int capturedEpoch);

    // ── Shared plumbing for the branches ─────────────────────────────────────

    protected static bool IsServer => MultiplayerSession.Instance?.Multiplayer.IsServer() == true;

    /// <summary>
    /// Queue this message's animations, honouring PlayAnimations and skipping headless entirely.
    ///
    /// Takes a factory rather than a list because the skip must prevent *construction*, not just
    /// enqueueing: evaluating an animation list builds presentation objects, and some read scene-tree
    /// singletons in their constructor (ReturnCameraAnimation reads InputManager.Current.Camera), which
    /// is null on a dedicated server. The enqueue itself already no-ops through the null sink.
    /// </summary>
    protected void EnqueueAnimations(Func<List<ChangeEventAnimation>> animations)
    {
        if (!PlayAnimations || GameContext.IsHeadless) return;

        foreach (ChangeEventAnimation anim in animations())
        {
            DebugUtilities.PrintPeer($"Enqueuing animation {anim.ScriptName} for {ScriptName} (Id: {Id})");
            _ = PresentationServices.Animation.Enqueue(anim);
        }
    }

    /// <summary>Append to the local replay log, which is also what the history UI reads.</summary>
    protected void RecordApplied() => MultiplayerSession.Instance?.GameState.GameMessages.Add(this);

    // ── Wire ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Serialize this message to its wire-format DTO.
    ///
    /// The DECLARED return type must stay GameMessageDto: that is where the [JsonPolymorphic]
    /// configuration lives, and System.Text.Json resolves polymorphism from the type it is handed at
    /// the Serialize call in BroadCast. Subclasses may narrow their own override covariantly for
    /// convenience, but BroadCast passes typeof(GameMessageDto) explicitly so a narrowed override can
    /// never silently drop the "$type" discriminator and break every client's FromDto.
    /// </summary>
    public abstract GameMessageDto ToDto();

    /// <summary>Reconstruct a message from its wire-format DTO (called on clients).</summary>
    public static GameMessage FromDto(GameMessageDto dto)
    {
        GameMessage msg = dto switch
        {
            // ── State mutations ──────────────────────────────────────────────
            DeployUnitChangeEventDto d       => new DeployUnitChangeEvent(d.TriggeringFaction, d.CountryId, d.DeploymentType),
            BattleCountryChangeEventDto d    => new BattleCountryChangeEvent(d.TriggeringFaction, d.CountryId),
            RemoveUnitChangeEventDto d       => new RemoveUnitChangeEvent(d.TriggeringFaction, d.UnitId, d.Reason),
            BattleUnitChangeEventDto d       => new BattleUnitChangeEvent(d.TriggeringFaction, d.UnitId),
            PlayCardChangeEventDto d         => new PlayCardChangeEvent(d.SourceCardId),
            ActivateReactionChangeEventDto d => new ActivateReactionChangeEvent(d.TriggeringFaction, d.SourceCardId, ChangeEvent.ForId(d.SourceChangeEventId)),
            // ModifiersApplied: d.NumberOfCards is the server's post-modifier count, so the client must
            // not run the IDiscardModifier pass again — see ForceDiscardCardsChangeEvent.
            ForceDiscardCardsChangeEventDto d     => new ForceDiscardCardsChangeEvent(d.TriggeringFaction, d.TargetFaction, d.NumberOfCards) { ModifiersApplied = true },
            DiscardHandCardsChangeEventDto d       => new DiscardHandCardsChangeEvent(d.TriggeringFaction, d.TargetFaction, d.CardIds),
            ForceDiscardHandCardsChangeEventDto d   => new ForceDiscardHandCardsChangeEvent(d.TriggeringFaction, d.TargetFaction, d.NumberOfCards),
            DrawCardsChangeEventDto d        => new DrawCardsChangeEvent(d.TriggeringFaction, d.TargetFaction, d.NumberOfCards, d.ShowDrawnCards),
            DrawCardByNameChangeEventDto d   => new DrawCardByNameChangeEvent(d.TriggeringFaction, d.TargetFaction, d.CardName),
            ScorePointsChangeEventDto d      => new ScorePointsChangeEvent(d.VPTurnSummary),
            SetStartingScoreChangeEventDto d => new SetStartingScoreChangeEvent(d.TriggeringFaction, d.Score),
            ChangeStepChangeEventDto d        => new ChangeStepChangeEvent(d.NewStep),
            ChangeRoundChangeEventDto d       => new ChangeRoundChangeEvent(d.NewTurn),
            RecycleCardChangeEventDto d        => new RecycleCardChangeEvent(d.TriggeringFaction, d.TargetFaction, d.CardId, d.Destination),
            SpendPlayActionChangeEventDto d    => new SpendPlayActionChangeEvent(d.TriggeringFaction),
            ReorderDeckChangeEventDto d        => new ReorderDeckChangeEvent(d.TriggeringFaction, d.ReorderedCardIds) { ReorderedFromTop = d.ReorderedFromTop },
            GrantSupplyChangeEventDto d         => new GrantSupplyChangeEvent(d.TriggeringFaction, d.UnitIds),
            RegisterBulletinCardChangeEventDto d => new RegisterBulletinCardChangeEvent(d.TargetFaction, d.CardId, d.MutatorClassName, d.FromRound, d.ToRound),

            // ── Derived-state delivery ───────────────────────────────────────
            RecalculateTagsMessageDto d     => new RecalculateTagsMessage(d.Snapshot),

            // ── Presentation only ────────────────────────────────────────────
            ShowBulletinPresentationEventDto d   => new ShowBulletinPresentationEvent(d.TriggeringFaction, d.Label, d.BulletinText),
            ShowActionLabelPresentationEventDto d => new ShowActionLabelPresentationEvent(d.TriggeringFaction, d.Text),

            _ => throw new NotSupportedException($"Unknown GameMessageDto type: {dto.GetType().Name}")
        };

        msg.ApplyDtoFields(dto);
        return msg;
    }

    /// <summary>
    /// Copy the wire fields the constructor above did not carry. Split from FromDto so each branch
    /// restores only the fields it owns — a PresentationEvent has no hash or reaction flags to restore.
    /// </summary>
    protected virtual void ApplyDtoFields(GameMessageDto dto)
    {
        Id                  = dto.Id;
        PlayAnimations      = dto.PlayAnimations;
        BlockAnimationQueue = dto.BlockAnimationQueue;
    }

    /// <summary>Hand this message to the ordered Rpc. Server-side only; a client never re-broadcasts.</summary>
    public Task BroadCast()
    {
        OnBeforeBroadcast();
        // Serialize against GameMessageDto explicitly rather than relying on generic inference: the
        // [JsonPolymorphic] config lives on that type, and passing it by hand means a covariant
        // ToDto() override can never cost us the "$type" discriminator.
        string dtoJson = JsonSerializer.Serialize(ToDto(), typeof(GameMessageDto));
        DebugUtilities.PrintPeer($"[color={"blue"}]Emitting {ScriptName} to clients (Id: {Id}{BroadcastLogDetail})");
        DebugUtilities.PrintPeerFinest($"{dtoJson}");
        NetworkApi.Instance.Rpc(nameof(NetworkApi.ReceiveGameMessage), dtoJson);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Last chance to stamp wire-only fields. ChangeEvent computes its post-mutation state hash here,
    /// which is why BroadCast has to run after ExecuteAsync rather than before.
    /// </summary>
    protected virtual void OnBeforeBroadcast() { }

    /// <summary>Extra detail for the broadcast log line — ChangeEvent appends its state hash.</summary>
    protected virtual string BroadcastLogDetail => string.Empty;

    // ── Display/debug ────────────────────────────────────────────────────────

    public virtual string TraceText() => ScriptName;

    public virtual string SummaryText() => ScriptName;

    public virtual string DebugText() => ScriptName;
}
