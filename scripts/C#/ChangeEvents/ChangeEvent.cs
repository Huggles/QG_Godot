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
public abstract partial class ChangeEvent : GameMessage
{
    public string HashAfterApplication { get; set; } = null;
    [JsonIgnore] public static int LatestAppliedId { get; set; } = -1;
    FactionState triggeringFactionState => FactionState.ForEnum(TriggeringFaction);
    FactionState targetFactionState => FactionState.ForEnum(TargetFaction);
    public bool SuppressGameProgress { get; set; } = false;
    public bool IsTrigger { get; set; } = true;
    public bool IsBlocked { get; set; } = false;
    public int SourceCardId { get; set; } = -1;
    public bool HasSourceCard => SourceCardId > -1;
    public CardState SourceCardState => CardState.ForId(SourceCardId);

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
            if (!ErrorReporter.BroadcastSent)
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
        if(CardPlayRound.Current != null)
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
        GameStateCalculator.CalculateAll();
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
