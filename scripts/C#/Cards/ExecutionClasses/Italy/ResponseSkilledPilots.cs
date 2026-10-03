using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class ResponseSkilledPilots : ResponseCardLogic
{
    // No Targets() override: this changes how many cards a discard costs, and a deck has no place
    // on the board. Nothing to preview.


    int NumberOfCardsReduction = 5;
    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.IsBlockRequest(), this),
            Condition.Build(new Condition.CustomCondition(s=>{
                if(s.BlockTrigger is ForceDiscardCardsChangeEvent ForceDiscardCardsChangeEvent){
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
            // A BlockStep that does not block. It is offered in the block window — that is the only
            // window whose trigger it can reach — but its effect is to make the discard SMALLER, so
            // it returns Nothing and lets the reduced event apply.
            new BlockStep<ForceDiscardCardsChangeEvent>(this, async discardEvent => {
                int newNumberOfCards = Math.Max(discardEvent.NumberOfCards - NumberOfCardsReduction, 0);
                discardEvent.NumberOfCards = newNumberOfCards;
                await new ShowActionLabelPresentationEvent(Faction, $"{Faction.WithPlayer()} plays Skilled Pilots: the discard is reduced by {NumberOfCardsReduction} to {newNumberOfCards} card(s)").Apply();
                await Task.Delay(GameSettings.DurationMedium);
                return CardStepResult.Nothing;
            })
        };
    }
}