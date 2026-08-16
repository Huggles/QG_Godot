using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class ResponseSkilledPilots : ResponseCardLogic
{

    int NumberOfCardsReduction = 5;
    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.IsBlockRequest(), this),
            Condition.Build(new Condition.CustomCondition(()=>{
                if(CardPlayPool.CurrentBlockTrigger is ForceDiscardCardsChangeEvent ForceDiscardCardsChangeEvent){
                    bool isEW = ForceDiscardCardsChangeEvent.SourceCardState.CardData.CardType == CardType.ECONOMIC_WARFARE;
                    bool targetIsMe = ForceDiscardCardsChangeEvent.TargetFaction == Faction;
                    return isEW && targetIsMe;
                }
                return false;
            }), this),

        };
    }
    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new CardStep(this, async ()=>{
                if(ActivationTrigger is ForceDiscardCardsChangeEvent ForceDiscardCardsChangeEvent){
                    int newNumberOfCards = Math.Max(ForceDiscardCardsChangeEvent.NumberOfCards - this.NumberOfCardsReduction, 0);
                    ForceDiscardCardsChangeEvent.NumberOfCards = newNumberOfCards;
                    PresentationServices.Notification.ShowActionText($"Reduced the number of cards to discard by {NumberOfCardsReduction} to a total of {newNumberOfCards}", Faction);
                    await Task.Delay(GameSettings.DurationMedium);
                    return;
                }else {
                    throw new Exception("Reaction should be to a discard change event");
                }
            })
        };
    }
}