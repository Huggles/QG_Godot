using Godot;
using System;
using System.Threading.Tasks;

public partial class DiscardCardsChangeEvent : ChangeEvent
{
    public int NumberOfCards { get; set; }
    public Faction TargetFaction { get; set; }

    public DiscardCardsChangeEvent(Faction triggeringFaction, Faction targetFaction, int numberOfCards) : base(triggeringFaction)
    {
        TriggeringFaction = triggeringFaction;
        TargetFaction = targetFaction;
        NumberOfCards = numberOfCards;
    }

    protected override async Task<bool> ExecuteAsync()
    {        
        DeckState deckState = DeckState.ForFaction(TargetFaction);
        deckState.DiscardTopCards(NumberOfCards);
        return true;
    }
}