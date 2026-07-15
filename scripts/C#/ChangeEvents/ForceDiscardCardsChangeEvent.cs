using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class ForceDiscardCardsChangeEvent : ChangeEvent
{
    public int NumberOfCards { get; set; }
    public List<int> DiscardedCardIds { get; private set; } = new();

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
        DiscardedCardIds = deckState.DiscardTopCards(NumberOfCards);
        return true;
    }

    protected override List<ChangeEventAnimation> AfterAnimations => new()
    {
        new ShowNotificationLabelAnimation($"{TriggeringFaction} makes {TargetFaction} discard {NumberOfCards} cards", TriggeringFaction),
        new ShowDiscardModalAnimation(DiscardedCardIds, "Discarded cards")
    };

    private void ApplyDiscardModifiers()
    {
        foreach (IDiscardModifier modifier in ModifierRegistry.GetAll<IDiscardModifier>())
        {
            int delta = modifier.ModifyDiscard(this);
            if (delta != 0)
                NumberOfCards = Math.Max(NumberOfCards + delta, 0);
        }
    }

    public override string SummaryText() => $"{TargetFaction} was forced to discard {NumberOfCards} cards by {TriggeringFaction}";
}