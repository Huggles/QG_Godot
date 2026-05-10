using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
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
        ApplyDiscardModifiers();

        DeckState deckState = DeckState.ForFaction(TargetFaction); 
        deckState.DiscardTopCards(NumberOfCards);
        PlayerActionLabel.ShowText($"{triggeringFactionState.FactionData.Label} makes {targetFactionState.FactionData.Label} discard {NumberOfCards} cards", TriggeringFaction);
        await Task.Delay(GameSettings.PauseDuration);
        return true;
    }

    private void ApplyDiscardModifiers()
    {
        var allStatusCardLogics = GameSession.Instance.GameState.FactionStates.Values
            .SelectMany(fs => DeckState.ForFaction(fs.FactionData.Faction).StatusCardStates)
            .Where(cs => cs.CardLogic.IsPlayed)
            .Select(cs => cs.CardLogic)
            .OfType<IDiscardModifier>();

        foreach (var modifier in allStatusCardLogics)
        {
            int delta = modifier.ModifyDiscard(this);
            if (delta != 0)
                NumberOfCards = Math.Max(NumberOfCards + delta, 0);
        }
    }
}