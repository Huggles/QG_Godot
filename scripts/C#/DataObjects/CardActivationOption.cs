using Godot;
using System;

public partial class CardActivationOption : GodotObject
{
    public int CardId { get; set; }
    public int ChangeEventId { get; set; }
    public string Type { get; set; }

    public CardState CardState {
        get { return CardState.ForId(CardId); }
    }
    public ChangeEvent ChangeEvent {
        get { return ChangeEvent.ForId(ChangeEventId); }
    }

    public CardActivationOption(int cardId, string type){
        this.CardId = cardId;
        this.Type = type;
    }
}
