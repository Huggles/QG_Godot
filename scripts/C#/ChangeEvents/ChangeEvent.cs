using Godot;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

public abstract partial class ChangeEvent : GodotObject, IChangeEvent
{
    static int changeEventCounter = 0;
    // Properties
    public string ScriptName => GetType().ToString();

    public int Id { get; set; } = -1;
    public Faction TriggeringFaction { get; set; }
    public bool SuppressGameProgress { get; set; } = false;
    public bool IsTrigger { get; set; } = true;
    public bool IsBlocked { get; set; } = false;
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
            PlayCardChangeEventDto d         => new PlayCardChangeEvent(d.TriggeringFaction, d.SourceCardId),
            ActivateReactionChangeEventDto d => new ActivateReactionChangeEvent(d.TriggeringFaction, d.SourceCardId, ForId(d.SourceChangeEventId)),
            DiscardCardsChangeEventDto d     => new DiscardCardsChangeEvent(d.TriggeringFaction, d.TargetFaction, d.NumberOfCards),
            DrawCardsChangeEventDto d        => new DrawCardsChangeEvent(d.TriggeringFaction, d.TargetFaction, d.NumberOfCards),
            _ => throw new NotSupportedException($"Unknown ChangeEventDto type: {dto.GetType().Name}")
        };
        ev.SourceCardId         = dto.SourceCardId;
        ev.IsTrigger            = dto.IsTrigger;
        ev.SuppressGameProgress = dto.SuppressGameProgress;
        return ev;
    }

    public async Task<bool> ApplyChange()
    {
        EventBus.Emit(EventBus.SignalName.GameChangeEventBefore);
        await ExecuteAsync();
        GameStateCalculator.CalculateAll();

        if (MultiplayerSession.Instance?.Multiplayer.IsServer() == true)
        {
            string dtoJson = JsonSerializer.Serialize(ToDto());
            DebugUtilities.PrintPeer($"{JsonSerializer.Serialize(MultiplayerSession.Instance.GameState)}", DebugVerbosity.INFO);
            string hash    = MultiplayerSession.Instance.GameState.ComputeHash();

            DebugUtilities.PrintPeer($"{MultiplayerSession.Instance.GameState.ComputeHash()}", DebugVerbosity.INFO);
            DebugUtilities.PrintPeer($"{MultiplayerSession.Instance.GameState.ComputeHash()}", DebugVerbosity.INFO);

            NetworkApi.Instance.Rpc(nameof(NetworkApi.ReceiveChangeEvent), dtoJson, hash);
        }

        EmitSignal(SignalName.ChangeEventApplied, Id);
        EventBus.Emit(EventBus.SignalName.GameChangeEventAfter, ScriptName);
        return true;
    }

    // Display/debug methods
    public virtual string TraceText() => ScriptName;

    public virtual string SummaryText() => ScriptName;

    public virtual string DebugText() => ScriptName;

    // Static lookup method
    public static ChangeEvent ForId(int changeEventId)
    {
        return MultiplayerSession.Instance?.GameState.GameChangeEvents.FirstOrDefault(ce => ce.Id == changeEventId)
            ?? GameSession.Current?.GameState.GameChangeEvents.FirstOrDefault(ce => ce.Id == changeEventId);
    }    
}
