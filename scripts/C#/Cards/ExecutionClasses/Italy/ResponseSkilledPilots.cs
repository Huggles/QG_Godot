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
                if(CardPlayPool.LastNoneNewCardChangeEvent is DiscardCardsChangeEvent discardCardsChangeEvent){
                    bool isEW = discardCardsChangeEvent.SourceCardState.CardData.CardType == CardType.ECONOMIC_WARFARE;
                    bool targetIsMe = discardCardsChangeEvent.TargetFaction == Faction;
                    return isEW && targetIsMe;
                }
                return false;
            }), this),

        };
    }
    public override List<CardStep> InitializeReactCardSteps()
    {
        return new List<CardStep>
        {
            new CardStep(this, async ()=>{
                if(CardPlayPool.LastNoneNewCardChangeEvent is DiscardCardsChangeEvent discardCardsChangeEvent){
                    int newNumberOfCards = Math.Max(discardCardsChangeEvent.NumberOfCards - this.NumberOfCardsReduction, 0);
                    discardCardsChangeEvent.NumberOfCards = newNumberOfCards;
                    PlayerActionLabel.ShowText($"Reduced the number of cards to discard by {NumberOfCardsReduction} to a total of {newNumberOfCards}", Faction);
                    await Task.Delay(GameSettings.PauseDuration);
                    return null;          
                }else {
                    throw new Exception("Reaction should be to a discard change event");
                }
            })
        };
    }
}