using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public abstract partial class StatusCardLogic : CardLogic
{
    public override List<CardStep> InitializePlayCardSteps()
    {
        return new List<CardStep> {
            new CardStep(this, async() => {
                DebugUtilities.PrintPeer($"StatusCardLogic.PlayCardStep for {CardState.CardName} (id={CardState.Id})");
                
                DeckState deckState = DeckState.ForFaction(Faction);
                DebugUtilities.PrintPeer($"  Moving card from HandCardIds (count={deckState.HandCardIds.Count}) to StatusCardIds (count={deckState.StatusCardIds.Count})");
                DebugUtilities.PrintPeer($"  Card is in hand: {deckState.HandCardIds.Contains(CardState.Id)}");
                deckState.StatusCardIds.Add(CardState.Id);
                deckState.HandCardIds.Remove(CardState.Id);
                DebugUtilities.PrintPeer($"  After move: HandCardIds={deckState.HandCardIds.Count}, StatusCardIds={deckState.StatusCardIds.Count}");
                DebugUtilities.PrintPeer($"  IsPlayed is now: {IsPlayed}");
                await Task.CompletedTask;
                return null;
            }) 
        }; 
    }
}
