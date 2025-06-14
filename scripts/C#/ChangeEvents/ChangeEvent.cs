using Godot;
using System;
using System.Diagnostics;
using System.Linq;
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

    public async Task<bool> ApplyChange()
    {
        await ExecuteAsync();
        EventBus.Emit(EventBus.SignalName.RecalculateStraights);
        EventBus.Emit(EventBus.SignalName.RecalculateSupply);
        EmitSignal(SignalName.ChangeEventApplied, Id);
        return true;
    }

    // Display/debug methods
    public virtual string TraceText() => ScriptName;

    public virtual string SummaryText() => ScriptName;

    public virtual string DebugText() => ScriptName;

    // Static lookup method
    public static ChangeEvent ForId(int changeEventId)
    {
        return GameSession.Instance.GameState.GameChangeEvents.FirstOrDefault(ce => ce.Id == changeEventId);
    }    
}
