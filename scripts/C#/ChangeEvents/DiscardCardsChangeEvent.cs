using Godot;
using System;
using System.Threading.Tasks;

public partial class DiscardCardsChangeEvent : ChangeEvent
{
    public int NumberOfCards { get; set; }
    public Faction TargetFaction { get; set; }
    FactionState triggeringFactionState => FactionState.ForEnum(TriggeringFaction);
    FactionState targetFactionState => FactionState.ForEnum(TargetFaction);

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
        PlayerActionLabel.ShowText($"{triggeringFactionState.FactionData.Label} makes {targetFactionState.FactionData.Label} discard {NumberOfCards} cards", TriggeringFaction);
        await Task.Delay(2000);
        return true;
    }
}