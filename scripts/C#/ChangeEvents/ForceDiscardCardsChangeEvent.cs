using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class ForceDiscardCardsChangeEvent : ChangeEvent
{
    public int NumberOfCards { get; set; }

    public ForceDiscardCardsChangeEvent(Faction triggeringFaction, Faction targetFaction, int numberOfCards) : base(triggeringFaction)
    {
        TriggeringFaction = triggeringFaction;
        TargetFaction = targetFaction;
        NumberOfCards = numberOfCards;
    }

    public override ChangeEventDto ToDto()
    {
        ForceDiscardCardsChangeEventDto dto = ChangeEventDto.Build<ForceDiscardCardsChangeEventDto>(this, Id);
        dto.NumberOfCards = NumberOfCards;
        return dto;
    }

    protected override async Task<bool> ExecuteAsync()
    {
        ApplyDiscardModifiers();

        DeckState deckState = DeckState.ForFaction(TargetFaction); 
        deckState.DiscardTopCards(NumberOfCards);
        PlayerActionLabel.ShowText($"{TriggeringFaction} makes {TargetFaction} discard {NumberOfCards} cards", TriggeringFaction);
        await Task.Delay(GameSettings.PauseDuration);
        return true;
    }

    private void ApplyDiscardModifiers()
    {
        var allStatusCardLogics = GameSession.Current.GameState.FactionStates
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