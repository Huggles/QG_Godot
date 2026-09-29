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
    /// Each one costs the target 1 VP — see <see cref="Mutate"/>.
    /// </summary>
    public int UndischargedCards { get; private set; } = 0;

    /// <summary>
    /// True when NumberOfCards is already the final, post-modifier count — set by ChangeEvent.FromDto,
    /// because ToDto() runs after Mutate() and therefore ships the modified number. Without this
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

    // The penalty the last mutation charged, for the scoreboard entry that follows it on the live board.
    private VPTurnSummary _penalty;

    public override void Mutate(BoardState board)
    {
        if (!ModifiersApplied)
            ApplyDiscardModifiers(board);
        DiscardedCardIds = board.DiscardTopCards(TargetFaction, NumberOfCards);

        // A card that cannot be discarded because the deck ran out costs the target 1 VP instead.
        // Applied here rather than as a nested ScorePointsChangeEvent: clients replay this
        // Mutate from the DTO, so a nested Apply would be both broadcast and replayed,
        // deducting twice. Recomputing the shortfall locally keeps the deduction inside the same
        // hashed mutation.
        UndischargedCards = NumberOfCards - DiscardedCardIds.Count;
        _penalty = null;
        if (UndischargedCards > 0)
        {
            _penalty = new VPTurnSummary(board.GameTurn, TargetFaction);
            _penalty.AddScore(new VPEntry(-UndischargedCards, $"{UndischargedCards} card(s) that could not be discarded from an empty deck"));
            board.AddScore(TargetFaction, _penalty.TotalScore);
        }
    }

    protected override Task OnLiveMutatedAsync()
    {
        if (_penalty != null) GameAPI.PresentScore(_penalty);
        return Task.CompletedTask;
    }

    protected override List<ChangeEventAnimation> AfterAnimations
    {
        get
        {
            List<ChangeEventAnimation> animations = new()
            {
                new ShowNotificationLabelAnimation($"{TriggeringFaction.WithPlayer()} makes {TargetFaction.WithPlayer()} discard {NumberOfCards} cards", TriggeringFaction),
                new ShowDiscardModalAnimation(DiscardedCardIds, "Discarded cards", TargetFaction)
            };
            if (UndischargedCards > 0)
                animations.Add(new ShowNotificationLabelAnimation($"{TargetFaction.WithPlayer()} loses {UndischargedCards} VP for {UndischargedCards} card(s) it could not discard", TargetFaction));
            return animations;
        }
    }

    private void ApplyDiscardModifiers(BoardState board)
    {
        NumberOfCards = ModifiedCount(board);
        ModifiersApplied = true;
    }

    /// <summary>The count after every IDiscardModifier, without changing this event. No modifier reads NumberOfCards.</summary>
    private int ModifiedCount(BoardState board)
    {
        int count = NumberOfCards;
        foreach (IDiscardModifier modifier in board.Modifiers<IDiscardModifier>())
        {
            int delta = modifier.ModifyDiscard(this);
            if (delta != 0)
                count = Math.Max(count + delta, 0);
        }
        return count;
    }

    /// <summary>Mirror of Mutate: top-of-deck discards, and 1 VP lost for each card the deck cannot pay.</summary>
    public override void Project(BoardProjection projection)
        => projection.DiscardFromDeck(TargetFaction, ModifiersApplied ? NumberOfCards : ModifiedCount(BoardState.Live));

    public override string SummaryText() => UndischargedCards > 0
        ? $"{TargetFaction.WithPlayer()} was forced to discard {NumberOfCards} cards by {TriggeringFaction.WithPlayer()}, but only had {DiscardedCardIds.Count} left and lost {UndischargedCards} VP"
        : $"{TargetFaction.WithPlayer()} was forced to discard {NumberOfCards} cards by {TriggeringFaction.WithPlayer()}";
}