using Godot;
using System;

public partial class CardActivationOption : GodotObject
{
    public int CardId { get; set; }
    public int StepId { get; set; }
    public int ChangeEventId { get; set; }
    public bool Activatable { get; set; }
    public string Label { get; set; }

    public CardState CardState
    {
        get { return CardState.ForId(CardId); }
    }
    public ChangeEvent ChangeEvent {
        get { return ChangeEvent.ForId(ChangeEventId); }
    }

    public CardActivationOption(int cardId, int stepId, string label, bool activatable = false)
    {
        CardId = cardId;
        Activatable = activatable;
        Label = label;
        StepId = stepId;
    }
}
