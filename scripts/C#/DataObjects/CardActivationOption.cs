using Godot;
using System;

public partial class CardActivationOption : ActivationOption
{
    public int CardId { get; set; }
    public int StepId { get; set; }
    public int ChangeEventId { get; set; }
    public bool Activatable { get; set; }

    public CardState CardState
    {
        get { return CardState.ForId(CardId); }
    }
    public ChangeEvent ChangeEvent {
        get { return ChangeEvent.ForId(ChangeEventId); }
    }

    public CardActivationOption(int cardId, int stepId, string label, bool activatable = false) : base(cardId, label)
    {
        CardId = cardId;
        Activatable = activatable;
        Label = label;
        StepId = stepId;
    }
}
