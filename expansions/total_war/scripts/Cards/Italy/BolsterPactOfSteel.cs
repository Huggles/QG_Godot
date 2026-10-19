using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// "Use at any time. Germany may discard between 0 and 5 cards from its hand, then Italy may take the same number
/// of random cards from its discard pile and place them on top of its draw deck." Played as "during your turn":
/// offered in Italy's play prompt and in every after-reaction window of Italy's turn.
/// </summary>
public partial class BolsterPactOfSteel : BolsterCardLogic
{
    private const int MaxCards = 5;

    /// <summary>Italy's discard pile, less this card: it is discarded when used and must not recycle itself.</summary>
    private List<int> Recoverable(BoardState board) => board.ForFaction(Faction).Discarded.Where(cardId => cardId != CardState.Id).ToList();

    /// <summary>How many cards Germany discarded to this card, and how many Italy has taken back so far, read from the round's pool.</summary>
    private int GermanDiscards(GameSituation s) => s.Pool.OfType<DiscardHandCardsChangeEvent>()
        .Where(discard => discard.SourceCardId == CardState.Id && discard.TargetFaction == Faction.GERMANY).Sum(discard => discard.CardIds.Count);
    private int Recovered(GameSituation s) => s.Pool.OfType<RecycleCardChangeEvent>().Count(recycle => recycle.SourceCardId == CardState.Id);

    protected override List<Condition> CardTriggers() => new()
    {
        Condition.Build(new Condition.IsFactionTurn(Faction), this),
        Condition.Build(new Condition.CustomCondition(s =>
            s.Board.ForFaction(Faction.GERMANY).Hand.Count > 0 && Recoverable(s.Board).Count > 0).InReactionWindow(), this)
    };

    public override List<CardStep> OnActivate() =>
        new List<CardStep> { GermanDiscardStep() }.Concat(Enumerable.Range(0, MaxCards).Select(_ => RecoverStep())).ToList();

    /// <summary>Germany chooses 0 to 5 cards from its hand to discard; choosing none ends the card.</summary>
    private CardStep GermanDiscardStep() => new ResultStep(this, async () => {
            List<int> hand = BoardState.Live.ForFaction(Faction.GERMANY).Hand.ToList();
            List<int> chosen = (await new InputRequest.SelectCardsRequestHandler(Faction.GERMANY, hand,
                "Pact of Steel: discard 0 to 5 cards for Italy to take back", 0, System.Math.Min(MaxCards, hand.Count)).BroadCast()).ResponseCardIds;
            if (chosen.Count == 0) throw new StepSkippedException();
            return new DiscardHandCardsChangeEvent(Faction, Faction.GERMANY, chosen);
        })
        .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => s.Board.ForFaction(Faction.GERMANY).Hand.Count > 0), this))
        .WithGuidance("Germany discards 0 to 5 cards from its hand");

    /// <summary>One random card from Italy's discard pile to the top of its deck, while Germany's discards are not yet matched. GameRandom, as Giuliani.</summary>
    private CardStep RecoverStep() => new ResultStep(this, async () => {
            List<int> recoverable = Recoverable(BoardState.Live);
            int cardId = recoverable[GameRandom.Next(recoverable.Count)];
            await Task.CompletedTask;
            return new RecycleCardChangeEvent(Faction, Faction, cardId, RecycleDestination.TopOfDeck);
        })
        .WithCondition(() => Condition.Build(new Condition.CustomCondition(s =>
            Recovered(s) < GermanDiscards(s) && Recoverable(s.Board).Count > 0), this))
        .WithGuidance("Put a random card from Italy's discard pile on top of its draw deck");
}
