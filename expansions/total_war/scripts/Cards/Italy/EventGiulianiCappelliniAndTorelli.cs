using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class EventGiulianiCappelliniAndTorelli : EventCardLogic
{
    // No Targets() override: the cards move between piles, which have no place on the board.

    public override List<CardStep> OnActivate() => new()
    {
        RecoverStep(Faction.GERMANY),
        RecoverStep(Faction.ITALY),
        RecoverStep(Faction.JAPAN),

        new ResultStep(this, Choose.Fixed(_ => new ScorePointsChangeEvent(new VPEntry(1, "Giuliani, Cappellini, and Torelli"), Faction)))
            .WithGuidance("Gain 1 Victory Point")
    };

    /// <summary>
    /// The country's discard pile, less this card: a played Event sits there while it resolves, and
    /// recycling itself would reset the card mid-play.
    /// </summary>
    private List<int> Recoverable(BoardState board, Faction axis) =>
        board.ForFaction(axis).Discarded.Where(cardId => cardId != CardState.Id).ToList();

    /// <summary>
    /// A random card from the country's discard pile goes on top of its draw deck. GameRandom, as
    /// EventOperationMagic; and a free-form step, not Choose.Fixed, whose factory previews may run and must not draw.
    /// </summary>
    private CardStep RecoverStep(Faction axis)
    {
        return new ResultStep(this, async () => {
            List<int> recoverable = Recoverable(BoardState.Live, axis);
            int cardId = recoverable[GameRandom.Next(recoverable.Count)];
            await Task.CompletedTask;
            return new RecycleCardChangeEvent(Faction, axis, cardId, RecycleDestination.TopOfDeck);
        })
        .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => Recoverable(s.Board, axis).Count > 0), this))
        .WithGuidance($"Put a random card from {axis.Label()}'s discard pile on top of its draw deck");
    }
}
