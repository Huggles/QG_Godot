using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

public partial class CardActivationOption
{    public int CardId { get; set; }
    public int StepId { get; set; }
    public int ChangeEventId { get; set; }
    public bool Activatable { get; set; }

    public CardState CardState => CardState.ForId(CardId);
    public ChangeEvent ChangeEvent => ChangeEvent.ForId(ChangeEventId);

    public CardActivationOption(int cardStepId) : this(CardStep.ForId(cardStepId)){}
    public CardActivationOption(CardStep cardStep)
    {   
        CardId = cardStep.CardLogic.CardState.Id;
        StepId = cardStep.Id;
        Activatable = cardStep.CardLogic.IsReaction
            ? cardStep.CardLogic.CanBeActivated() || cardStep.CardLogic.IsActivatedThisTurn
            : cardStep.CardLogic.CanPlayCard();
    }

    public static List<CardActivationOption> FromCardStepIds(List<int> cardStepIds)
    {        
        return CardStep.ForIds(cardStepIds).Select(cs => new CardActivationOption(cs)).ToList();
    }
}
