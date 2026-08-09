using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

public abstract partial class ChangeEvent : GodotObject, IChangeEvent
{
    static int changeEventCounter = 0;
    // Properties
    public string ScriptName => GetType().ToString();

    public int Id { get; set; } = -1;

    public string HashAfterApplication { get; set; } = null;
    [JsonIgnore] public static int LatestAppliedId { get; set; } = -1;
    public Faction TriggeringFaction { get; set; }        
    public Faction TargetFaction { get; set; }
    FactionState triggeringFactionState => FactionState.ForEnum(TriggeringFaction);
    FactionState targetFactionState => FactionState.ForEnum(TargetFaction);
    public bool SuppressGameProgress { get; set; } = false;
    public bool IsTrigger { get; set; } = true;
    public bool IsBlocked { get; set; } = false;
    public bool PlayAnimations { get; set; } = true;
    /// <summary>When false, animations are fire-and-forget — ApplyChange does not wait for them to finish.</summary>
    public bool BlockAnimationQueue { get; set; } = true;
    public int SourceCardId { get; set; } = -1;
    public bool HasSourceCard => SourceCardId > -1;
    public CardState SourceCardState => CardState.ForId(SourceCardId);

    public virtual bool ToHistoryItem => true; // whether this event should be converted to a GameHistoryItem and displayed in the game history UI

    // Signal
    [Signal] public delegate void ChangeEventAppliedEventHandler(int changeEventId);

    // Constructor
    public ChangeEvent(Faction triggeringFaction)
    {
        TriggeringFaction = triggeringFaction;
        changeEventCounter += 1;
        Id = changeEventCounter;        
    }

    protected abstract Task<bool> ExecuteAsync();

    protected virtual List<ChangeEventAnimation> BeforeAnimations { get; } = new();
    protected virtual List<ChangeEventAnimation> AfterAnimations  { get; } = new();

    /// <summary>Serialize this event to its wire-format DTO for multiplayer replication.</summary>
    public abstract ChangeEventDto ToDto();

    /// <summary>Reconstruct a ChangeEvent from its wire-format DTO (called on clients).</summary>
    public static ChangeEvent FromDto(ChangeEventDto dto)
    {
        ChangeEvent ev = dto switch
        {
            DeployUnitChangeEventDto d       => new DeployUnitChangeEvent(d.TriggeringFaction, d.CountryId, d.DeploymentType),
            BattleCountryChangeEventDto d    => new BattleCountryChangeEvent(d.TriggeringFaction, d.CountryId),
            RemoveUnitChangeEventDto d       => new RemoveUnitChangeEvent(d.TriggeringFaction, d.UnitId, d.Reason),
            BattleUnitChangeEventDto d       => new BattleUnitChangeEvent(d.TriggeringFaction, d.UnitId),
            PlayCardChangeEventDto d         => new PlayCardChangeEvent(d.SourceCardId),
            ActivateReactionChangeEventDto d => new ActivateReactionChangeEvent(d.TriggeringFaction, d.SourceCardId, ForId(d.SourceChangeEventId)),
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
            ReorderDeckChangeEventDto d        => new ReorderDeckChangeEvent(d.TriggeringFaction, d.ReorderedCardIds),
            GrantSupplyChangeEventDto d         => new GrantSupplyChangeEvent(d.TriggeringFaction, d.UnitIds),
            RecalculateTagsChangeEventDto d     => new RecalculateTagsChangeEvent(d.Snapshot),
            RegisterBulletinCardChangeEventDto d => new RegisterBulletinCardChangeEvent(d.TargetFaction, d.CardId, d.MutatorClassName, d.FromRound, d.ToRound),
            _ => throw new NotSupportedException($"Unknown ChangeEventDto type: {dto.GetType().Name}")
        };
        ev.Id                   = dto.Id;
        ev.HashAfterApplication = dto.HashAfterApplication;
        ev.SourceCardId         = dto.SourceCardId;
        ev.IsTrigger            = dto.IsTrigger;
        ev.SuppressGameProgress = dto.SuppressGameProgress;
        ev.PlayAnimations        = dto.PlayAnimations;
        ev.BlockAnimationQueue   = dto.BlockAnimationQueue;
        
        return ev;
    }

    public virtual async Task<bool> ApplyChange()
    {
        // Mutation tracking for error classification. A throw between ExecuteAsync() (which mutates
        // server state) and BroadCast() (which tells the clients) leaves the peers already divergent,
        // and the resync repair in NetworkApi.RequestResync is commented out — so recovery cannot be
        // offered for that window. MutationDepth/BroadcastSent is how ErrorReporter detects it.
        int capturedEpoch = ErrorReporter.GameLoopEpoch;
        ErrorReporter.MutationDepth++;
        bool broadcastSentOnEntry = ErrorReporter.BroadcastSent;
        ErrorReporter.BroadcastSent = false;
        try
        {
            return await ApplyChangeInternal(capturedEpoch);
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

    private async Task<bool> ApplyChangeInternal(int capturedEpoch)
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
        // Skip on headless: evaluating the animation lists constructs presentation objects (e.g.
        // ReturnCameraAnimation reads InputManager.Current.Camera in its ctor), which is null on a
        // dedicated server. The enqueue itself already no-ops via the null sink; this avoids the
        // construction too.
        if (PlayAnimations && !GameContext.IsHeadless)
        {
            foreach (ChangeEventAnimation anim in BeforeAnimations)
                _ = PresentationServices.Animation.Enqueue(anim);
        }
        DebugUtilities.PrintPeer($"Execute Async");           
        await ExecuteAsync();
        // Armed after the mutation but before BroadCast() — the tier 3 divergence window.
        ErrorInjection.MaybeThrow(ErrorInjection.Site.MidMutation, ScriptName);
        // Broadcast this change event to clients BEFORE recalculating tags, so the tags snapshot
        // that CalculateAll() broadcasts (as a RecalculateTagsChangeEvent) is enqueued on clients
        // right behind this event and always applies to post-change state — never before it.
        bool isServer = MultiplayerSession.Instance?.Multiplayer.IsServer() == true;
        if (isServer)
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
        if (PlayAnimations && !GameContext.IsHeadless)
        {
            foreach (ChangeEventAnimation anim in AfterAnimations)
            {
                DebugUtilities.PrintPeer($"Enqueuing animation {anim.ScriptName} for change event {ScriptName} (Id: {Id})");
                _ = PresentationServices.Animation.Enqueue(anim);
            }
        }
        DebugUtilities.PrintPeer($"Awaiting)");
        await PresentationServices.Animation.Start(); // ensure queue is processing (no-op if already running / headless)
        DebugUtilities.PrintPeer($"Continue");
        
        MultiplayerSession.Instance?.GameState.GameChangeEvents.Add(this);
        LatestAppliedId = Id;
        EventBus.Emit(EventBus.SignalName.GameChangeEventAfter, ScriptName);        
        DebugUtilities.PrintPeer($"GameChangeEventAfter");
        
        return true;
    }


    // Display/debug methods
    public virtual string TraceText() => ScriptName;

    public virtual string SummaryText() => ScriptName;

    public virtual string DebugText() => ScriptName;

    public GameHistoryItem ToGameHistoryItem()
    {
        return ToHistoryItem ? GameHistoryItem.Create(SummaryText(), TriggeringFaction) : null;
    }

    public async Task BroadCast()
    {        
        this.HashAfterApplication = MultiplayerSession.Instance.GameState.ComputeHash();   
        string dtoJson = JsonSerializer.Serialize(ToDto());         
        DebugUtilities.PrintPeer($"[color={"blue"}]Emitting ChangeEvent to clients: {ScriptName} (Id: {Id}, Hash: {HashAfterApplication})");            
        DebugUtilities.PrintPeerFinest($"{dtoJson}");
        NetworkApi.Instance.Rpc(nameof(NetworkApi.ReceiveChangeEvent), dtoJson);
    }

    // Static lookup method
    public static ChangeEvent ForId(int changeEventId)
    {
        return MultiplayerSession.Instance?.GameState.GameChangeEvents.FirstOrDefault(ce => ce.Id == changeEventId)
            ?? GameSession.Current?.GameState.GameChangeEvents.FirstOrDefault(ce => ce.Id == changeEventId);
    }    
}
