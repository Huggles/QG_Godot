using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

/// <summary>
/// A <see cref="GameMessage"/> that mutates authoritative game state. This is the branch of the
/// replicated stream that is hashed, registered in the card play pool, recalculates tags and is tracked
/// for divergence — everything a mutation needs and a presentation-only message must not have.
///
/// For a message whose whole effect is what the player sees, use <see cref="PresentationEvent"/>
/// instead: it rides the same ordered channel without any of the machinery below.
/// </summary>
public abstract partial class ChangeEvent : GameMessage, ITargetSetProvider
{
    public string HashAfterApplication { get; set; } = null;
    [JsonIgnore] public static int LatestAppliedId { get; set; } = -1;
    FactionState triggeringFactionState => FactionState.ForEnum(TriggeringFaction);
    FactionState targetFactionState => FactionState.ForEnum(TargetFaction);
    public bool SuppressGameProgress { get; set; } = false;
    public bool IsTrigger { get; set; } = true;

    /// <summary>
    /// Which sources of derived state this event's <c>ExecuteAsync</c> disturbs, and so how much of
    /// the tag recalculation that follows it actually has to run.
    ///
    /// Defaults to <see cref="RecalcScope.All"/> — the behaviour every event had before this existed.
    /// Override it only where you can say precisely what the event touches, and describe only what
    /// THIS event's own ExecuteAsync does: effects a card goes on to cause arrive as their own nested
    /// ChangeEvents, each carrying its own scope, so the scopes compose without anyone having to
    /// reason about the whole chain.
    ///
    /// Getting one wrong leaves a stale tag rather than throwing, which is why the bar for narrowing
    /// is high and the default is deliberately pessimistic.
    /// </summary>
    [JsonIgnore] public virtual RecalcScope RecalcScope => RecalcScope.All;

    /// <summary>
    /// Whether this event joins the round's <c>ChangeEventsPool</c>. False for a mechanical mutation
    /// that is not a game event at all: nothing may react to it AND nothing may read it back as
    /// history. <see cref="IsTrigger"/> = false only closes the block and after-reaction windows;
    /// this additionally keeps the event out of the pool, out of <c>LastChangeEvent</c> /
    /// <c>LastNoneNewCardChangeEvent</c>, and so out of every pool-scoped <c>Condition</c>.
    ///
    /// Replicated, because a client evaluates conditions against its own pool — a flag the host
    /// honoured and the client did not would let the two peers disagree about what is reactable.
    /// </summary>
    public bool RegisterInPool { get; set; } = true;

    /// <summary>
    /// Whether the change event was blocked by a card reaction. This is set by the card logic that blocks the event, and is used to prevent the event from being applied.
    /// </summary>
    public bool IsBlocked { get; set; } = false;

    /// <summary>
    /// Whether the entire card was blocked by a card reaction. This is set by the card logic that blocks the event, and is used to prevent the event from being applied.
    /// In the base game, this happens only with ResponseASWTactics, but in expansion (or mods) it could happen with other cards as well.
    /// </summary>
    public bool IsCardBlocked { 
        get { return SourceCardState.IsBlocked; }
        set { SourceCardState.IsBlocked = value; }
    }


    public int SourceCardId { get; set; } = -1;
    public bool HasSourceCard => SourceCardId > -1;
    public CardState SourceCardState => CardState.ForId(SourceCardId);

    /// <summary>
    /// Where on the board this event lands, for the presentation that has to point at it — today the
    /// focus viewport beside a reaction window's trigger card (see <c>TriggerContextDisplay</c>).
    ///
    /// The same contract <see cref="CardLogic"/> implements, and the tense difference is deliberate:
    /// a card answers "what would I affect if used now", an event answers "what am I affecting". Both
    /// are presentation only — nothing in the execution path reads a <see cref="TargetSet"/>.
    ///
    /// Declaring nothing is the correct answer for every event that does not name a place, which is
    /// most of them, so the base returns <see cref="TargetSet.None"/> rather than making this
    /// abstract. Overridden by the deploy, battle and removal events.
    ///
    /// Read through <see cref="ITargetSetProviderExtensions.TargetsOrNone"/>, never bare: an override
    /// resolving a unit that has just been removed can legitimately throw, and that must cost the
    /// preview and not the prompt it rides on.
    /// </summary>
    public virtual TargetSet Targets() => TargetSet.None;

    // Signal
    [Signal] public delegate void ChangeEventAppliedEventHandler(int changeEventId);

    // Constructor
    public ChangeEvent(Faction triggeringFaction) : base(triggeringFaction) { }

    protected abstract Task<bool> ExecuteAsync();

    protected virtual List<ChangeEventAnimation> BeforeAnimations { get; } = new();
    protected virtual List<ChangeEventAnimation> AfterAnimations  { get; } = new();

    /// <summary>
    /// Narrowed covariantly from GameMessage.ToDto() purely for subclass convenience — every ChangeEvent
    /// returns a ChangeEventDto. BroadCast still serializes against GameMessageDto explicitly, so this
    /// narrowing cannot cost the "$type" discriminator.
    /// </summary>
    public abstract override ChangeEventDto ToDto();

    protected override void ApplyDtoFields(GameMessageDto dto)
    {
        base.ApplyDtoFields(dto);
        if (dto is not ChangeEventDto d) return;

        HashAfterApplication = d.HashAfterApplication;
        SourceCardId         = d.SourceCardId;
        IsTrigger            = d.IsTrigger;
        SuppressGameProgress = d.SuppressGameProgress;
        RegisterInPool       = d.RegisterInPool;
    }

    /// <summary>The state hash a client compares against after replaying this event.</summary>
    protected override void OnBeforeBroadcast()
        => HashAfterApplication = MultiplayerSession.Instance.GameState.ComputeHash();

    protected override string BroadcastLogDetail => $", Hash: {HashAfterApplication}";

    protected override async Task<bool> ApplyInternal(int capturedEpoch)
    {
        // Mutation tracking for error classification. A throw between ExecuteAsync() (which mutates
        // server state) and BroadCast() (which tells the clients) leaves the peers already divergent,
        // and the resync repair in NetworkApi.RequestResync is commented out — so recovery cannot be
        // offered for that window. MutationDepth/BroadcastSent is how ErrorReporter detects it.
        //
        // This wrapper lives on ChangeEvent, not GameMessage: a PresentationEvent has no divergence
        // window, because it has nothing to diverge.
        ErrorReporter.MutationDepth++;
        bool broadcastSentOnEntry = ErrorReporter.BroadcastSent;
        ErrorReporter.BroadcastSent = false;
        try
        {
            return await ApplyMutation(capturedEpoch);
        }
        catch (Exception e)
        {
            // Tag the exception here rather than letting ErrorReporter read MutationDepth at report
            // time: this finally block unwinds the counters as the stack unwinds, so by the time the
            // Guard catch at the top of the loop sees the exception, the state that identified the
            // divergence window is already gone. The marker travels with the exception instead.
            //
            // GameRuleException is excluded because "escaped before BroadCast" is only a PROXY for
            // "mutated but did not replicate", and for this one type the proxy is wrong: a rule
            // refusal is thrown by a precondition guard that runs before anything is touched (see
            // GameRuleException's own summary, and the "Checked before anything mutates" comment on
            // GameAPI.DeployUnitToCountry). Marking it diverged made ErrorReporter.Classify call a
            // harmless refusal Unrecoverable, which stops the turn loop instead of offering the popup
            // + Continue those refusals are designed around — UnitPool.RecallCandidates knowingly
            // leaves one such case in precisely because it was believed to be recoverable.
            //
            // The contract this relies on: a GameRuleException MUST be thrown before mutating state.
            // All three throw sites honour it (GameAPI.DeployUnitToCountry,
            // UnitPool.GetAvailableUnitForFaction, UnitPoolShortfall.ResolveBeforeDeploy). A new throw
            // site that mutates first would be silently misclassified as recoverable — throw a plain
            // Exception there instead.
            if (!ErrorReporter.BroadcastSent && e is not GameRuleException)
                ErrorReporter.MarkDiverged(e);
            throw;
        }
        finally
        {
            ErrorReporter.MutationDepth--;
            ErrorReporter.BroadcastSent = broadcastSentOnEntry;
        }
    }

    private async Task<bool> ApplyMutation(int capturedEpoch)
    {
        // If the loop was recovered while this was in flight, unwind instead of mutating state
        // alongside the resumed loop.
        ErrorReporter.ThrowIfStaleEpoch(capturedEpoch);

        EventBus.Emit(EventBus.SignalName.GameChangeEventBefore);
        DebugUtilities.PrintPeer($"Doing change event {ScriptName} (Id: {Id}) with source card {SourceCardId} and triggering faction {TriggeringFaction}");
        if(RegisterInPool && CardPlayRound.Current != null)
        {
            DebugUtilities.PrintPeer($"Registering change event {ScriptName} (Id: {Id}) with current CardPlayRound");
            CardPlayRound.Current.RegisterChangeEvent(this);
        }
        DebugUtilities.PrintPeer($"PlayAnimations: {PlayAnimations}, BlockAnimationQueue: {BlockAnimationQueue}");
        EnqueueAnimations(() => BeforeAnimations);
        DebugUtilities.PrintPeer($"Execute Async");
        await ExecuteAsync();
        // Armed after the mutation but before BroadCast() — the tier 3 divergence window.
        ErrorInjection.MaybeThrow(ErrorInjection.Site.MidMutation, ScriptName);
        // Broadcast this change event to clients BEFORE recalculating tags, so the tags snapshot
        // that CalculateAll() broadcasts (as a RecalculateTagsMessage) is enqueued on clients
        // right behind this event and always applies to post-change state — never before it.
        if (IsServer)
        {
            await BroadCast();
        }
        // From here on the clients have the change, so a later failure is recoverable: the remaining
        // work is a tag recalculation and animations, and tags are not part of the replicated hash.
        // On a client there is nothing to broadcast — it is replaying an event the server already
        // sent — so the divergence window does not apply and this is set unconditionally.
        ErrorReporter.BroadcastSent = true;
        GameStateCalculator.CalculateAll(RecalcScope);
        EmitSignal(SignalName.ChangeEventApplied, Id);
        EnqueueAnimations(() => AfterAnimations);
        DebugUtilities.PrintPeer($"Awaiting)");
        await PresentationServices.Animation.Start(); // ensure queue is processing (no-op if already running / headless)
        DebugUtilities.PrintPeer($"Continue");

        RecordApplied();
        LatestAppliedId = Id;
        EventBus.Emit(EventBus.SignalName.GameChangeEventAfter, ScriptName);
        DebugUtilities.PrintPeer($"GameChangeEventAfter");

        return true;
    }

    /// <summary>
    /// Static lookup by stream id. Filters to ChangeEvents because the log it scans holds every
    /// GameMessage, and the only caller (ActivateReactionChangeEvent's reaction source) wants a
    /// mutation.
    /// </summary>
    public static ChangeEvent ForId(int changeEventId)
    {
        return MultiplayerSession.Instance?.GameState.GameMessages.OfType<ChangeEvent>().FirstOrDefault(ce => ce.Id == changeEventId)
            ?? GameSession.Current?.GameState.GameMessages.OfType<ChangeEvent>().FirstOrDefault(ce => ce.Id == changeEventId);
    }
}
