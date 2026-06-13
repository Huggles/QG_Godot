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
    public int SourceCardId { get; set; } = -1;
    public bool HasSourceCard => SourceCardId > -1;
    public CardState SourceCardState => CardState.ForId(SourceCardId);

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
            DiscardHandCardsChangeEventDto d     => new DiscardHandCardsChangeEvent(d.TriggeringFaction, d.TargetFaction, d.CardIds),
            DrawCardsChangeEventDto d        => new DrawCardsChangeEvent(d.TriggeringFaction, d.TargetFaction, d.NumberOfCards, d.ShowDrawnCards),
            DrawCardByNameChangeEventDto d   => new DrawCardByNameChangeEvent(d.TriggeringFaction, d.TargetFaction, d.CardName),
            ScorePointsChangeEventDto d      => new ScorePointsChangeEvent(d.VPTurnSummary),
            _ => throw new NotSupportedException($"Unknown ChangeEventDto type: {dto.GetType().Name}")
        };
        ev.Id                   = dto.Id;
        ev.HashAfterApplication = dto.HashAfterApplication;
        ev.SourceCardId         = dto.SourceCardId;
        ev.IsTrigger            = dto.IsTrigger;
        ev.SuppressGameProgress = dto.SuppressGameProgress;
        ev.PlayAnimations        = dto.PlayAnimations;
        
        return ev;
    }

    public async Task<bool> ApplyChange()
    {
        // Evaluate both lists upfront so constructors (e.g. ReturnCameraAnimation)
        // capture state before any animation runs.
        var beforeAnims = BeforeAnimations;
        var afterAnims  = AfterAnimations;

        EventBus.Emit(EventBus.SignalName.GameChangeEventBefore);        
        if (PlayAnimations)
        {
            DebugUtilities.PrintPeer("Doing before animations for " + ScriptName);
            foreach (var anim in beforeAnims)
                await anim.Execute();
        }
        await ExecuteAsync();
        GameStateCalculator.CalculateAll();
        if (PlayAnimations)
        {
            
            foreach (var anim in afterAnims)
                await anim.Execute();
        }

        if (MultiplayerSession.Instance?.Multiplayer.IsServer() == true)
        {   
            await BroadCast();
        }
        LatestAppliedId = Id;
        EmitSignal(SignalName.ChangeEventApplied, Id);        
        EventBus.Emit(EventBus.SignalName.GameChangeEventAfter, ScriptName);        
        return true;
    }


    // Display/debug methods
    public virtual string TraceText() => ScriptName;

    public virtual string SummaryText() => ScriptName;

    public virtual string DebugText() => ScriptName;

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
