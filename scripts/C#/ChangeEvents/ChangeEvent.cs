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
            ForceDiscardCardsChangeEventDto d     => new ForceDiscardCardsChangeEvent(d.TriggeringFaction, d.TargetFaction, d.NumberOfCards),
            DiscardHandCardsChangeEventDto d       => new DiscardHandCardsChangeEvent(d.TriggeringFaction, d.TargetFaction, d.CardIds),
            ForceDiscardHandCardsChangeEventDto d   => new ForceDiscardHandCardsChangeEvent(d.TriggeringFaction, d.TargetFaction, d.NumberOfCards),
            DrawCardsChangeEventDto d        => new DrawCardsChangeEvent(d.TriggeringFaction, d.TargetFaction, d.NumberOfCards, d.ShowDrawnCards),
            DrawCardByNameChangeEventDto d   => new DrawCardByNameChangeEvent(d.TriggeringFaction, d.TargetFaction, d.CardName),
            ScorePointsChangeEventDto d      => new ScorePointsChangeEvent(d.VPTurnSummary),
            ChangeStepChangeEventDto d        => new ChangeStepChangeEvent(d.NewStep),
            ChangeRoundChangeEventDto d       => new ChangeRoundChangeEvent(d.NewTurn),
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

    public async Task<bool> ApplyChange()
    {
        EventBus.Emit(EventBus.SignalName.GameChangeEventBefore);              
        if(CardPlayRound.Current != null)
        {
            DebugUtilities.PrintPeer($"Registering change event {ScriptName} (Id: {Id}) with current CardPlayRound");
            CardPlayRound.Current.RegisterChangeEvent(this);
        }
        foreach (var anim in BeforeAnimations)
        {
            if(PlayAnimations)
            {
                _ = AnimationQueue.Instance.Enqueue(anim);            
            }
            
        }
        await ExecuteAsync();        
        GameStateCalculator.CalculateAll();
        EmitSignal(SignalName.ChangeEventApplied, Id);       
        if (MultiplayerSession.Instance?.Multiplayer.IsServer() == true)
        {   
            await BroadCast();
        }
        foreach (var anim in AfterAnimations)
        {
            if(PlayAnimations)
            {
                _ = AnimationQueue.Instance.Enqueue(anim);            
            }
            
        }        
        await AnimationQueue.Instance.Start(); // ensure queue is processing (no-op if already running)
        
        MultiplayerSession.Instance?.GameState.GameChangeEvents.Add(this);
        LatestAppliedId = Id;
        EventBus.Emit(EventBus.SignalName.GameChangeEventAfter, ScriptName);        
        
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
