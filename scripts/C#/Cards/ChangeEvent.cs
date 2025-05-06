using Godot;
using System;
using System.Linq;

public abstract partial class ChangeEvent
{
    // Properties
    public string ScriptName => GetType().ToString();

    public int Id { get; set; } = -1;
    public Faction TriggeringFaction { get; set; }
    public bool SuppressGameProgress { get; set; } = false;
    public bool IsTrigger { get; set; } = true;
    public int SourceCardId { get; set; } = -1;
    public bool HasSourceCard => SourceCardId > -1;
    public CardState SourceCardState => CardState.ForId(SourceCardId);
    public bool Blocked { get; set; } = false;

    // Signal
    [Signal]
    public delegate void FinishedEventHandler();

    // Constructor
    public ChangeEvent(Faction triggeringFaction)
    {
        TriggeringFaction = triggeringFaction;
    }

    // Main logic
    public void ApplyChange()
    {
        ApplyChangeEvent();
        EventBus.Emit("GameChangeEventOccurred", this.Id);
        EventBus.Emit("RecalculateStraights", this.Id);
        EventBus.Emit("RecalculateSupply", this.Id);        
    }

    protected virtual void ApplyChangeEvent()
    {
        // To be overridden in subclasses
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
