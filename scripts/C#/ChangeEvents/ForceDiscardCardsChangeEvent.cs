using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class ForceDiscardCardsChangeEvent : ChangeEvent
{
    public int NumberOfCards { get; set; }
    public List<int> DiscardedCardIds { get; private set; } = new();

    /// <summary>
    /// Cards the target was required to discard but could not, because its draw deck ran out.
    /// Each one costs the target 1 VP — see <see cref="ExecuteAsync"/>.
    /// </summary>
    public int UndischargedCards { get; private set; } = 0;

    /// <summary>
    /// True when NumberOfCards is already the final, post-modifier count — set by ChangeEvent.FromDto,
    /// because ToDto() runs after ExecuteAsync() and therefore ships the modified number. Without this
    /// the client re-ran ApplyDiscardModifiers() on top of the server's result and applied every delta
    /// twice: a guaranteed desync, and with a negative modifier (e.g. Jet Fighters, -3) the count
    /// clamped to 0, so the client discarded nothing and ShowDiscardModalAnimation got an empty list.
    /// </summary>
    public bool ModifiersApplied { get; set; } = false;

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
        if (!ModifiersApplied)
            ApplyDiscardModifiers();
        DeckState deckState = DeckState.ForFaction(TargetFaction);
        DiscardedCardIds = deckState.DiscardTopCards(NumberOfCards);

        // A card that cannot be discarded because the deck ran out costs the target 1 VP instead.
        // Applied here rather than as a nested ScorePointsChangeEvent: clients replay this
        // ExecuteAsync from the DTO, so a nested Apply would be both broadcast and replayed,
        // deducting twice. Recomputing the shortfall locally keeps the deduction inside the same
        // hashed mutation, exactly like ForceDiscardHandCardsChangeEvent calling GameAPI directly.
        UndischargedCards = NumberOfCards - DiscardedCardIds.Count;
        if (UndischargedCards > 0)
        {
            VPTurnSummary penalty = new VPTurnSummary(GameFlow.Instance.GameTurn, TargetFaction);
            penalty.AddScore(new VPEntry(-UndischargedCards, $"{UndischargedCards} card(s) that could not be discarded from an empty deck"));
            GameAPI.ScorePoints(penalty);
        }
        return true;
    }

    protected override List<ChangeEventAnimation> AfterAnimations
    {
        get
        {
            List<ChangeEventAnimation> animations = new()
            {
                new ShowNotificationLabelAnimation($"{TriggeringFaction} makes {TargetFaction} discard {NumberOfCards} cards", TriggeringFaction),
                new ShowDiscardModalAnimation(DiscardedCardIds, "Discarded cards", TargetFaction)
            };
            if (UndischargedCards > 0)
                animations.Add(new ShowNotificationLabelAnimation($"{TargetFaction} loses {UndischargedCards} VP for {UndischargedCards} card(s) it could not discard", TargetFaction));
            return animations;
        }
    }

    private void ApplyDiscardModifiers()
    {
        foreach (IDiscardModifier modifier in ModifierRegistry.GetAll<IDiscardModifier>())
        {
            int delta = modifier.ModifyDiscard(this);
            if (delta != 0)
                NumberOfCards = Math.Max(NumberOfCards + delta, 0);
        }
        ModifiersApplied = true;
    }

    public override string SummaryText() => UndischargedCards > 0
        ? $"{TargetFaction} was forced to discard {NumberOfCards} cards by {TriggeringFaction}, but only had {DiscardedCardIds.Count} left and lost {UndischargedCards} VP"
        : $"{TargetFaction} was forced to discard {NumberOfCards} cards by {TriggeringFaction}";
}