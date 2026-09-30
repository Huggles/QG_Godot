using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The board plus what is happening on it right now — the whole of what a condition or a step choice
/// may read. Blitzkrieg's trigger is the canonical case: "Germany just battled a land space (WHICH
/// battle — the reaction trigger) and can build there (the board)".
///
/// The trigger, the round's event pool and the reaction depth are not board data: they belong to the
/// round in progress (CardPlayRound). Keeping them here rather than on <see cref="BoardState"/> keeps
/// the board pure positional data, so one fork can be asked about different triggers.
///
/// <see cref="Live"/> is the situation the game is in. A bot asking "after this battle, would
/// Blitzkrieg fire?" builds <c>GameSituation.Live.WithBoard(fork).ReactingTo(battle)</c>.
/// </summary>
public sealed class GameSituation
{
    private static readonly List<ChangeEvent> NoEvents = new();
    private static readonly List<CardState> NoCards = new();

    public BoardState Board { get; }

    /// <summary>The event the open after-reaction window reacts to, or null.</summary>
    public ChangeEvent ReactionTrigger { get; }

    /// <summary>The event the open block window offers for block, or null.</summary>
    public ChangeEvent BlockTrigger { get; }

    /// <summary>The events applied so far this round, oldest first.</summary>
    public IReadOnlyList<ChangeEvent> Pool { get; }

    /// <summary>The cards played or activated this round.</summary>
    public IReadOnlyList<CardState> CardPool { get; }

    /// <summary>How many card activations are running; above 0 only event-triggered cards may activate.</summary>
    public int ReactionDepth { get; }

    private GameSituation(BoardState board, ChangeEvent reactionTrigger, ChangeEvent blockTrigger,
                          IReadOnlyList<ChangeEvent> pool, IReadOnlyList<CardState> cardPool, int reactionDepth)
    {
        Board = board;
        ReactionTrigger = reactionTrigger;
        BlockTrigger = blockTrigger;
        Pool = pool;
        CardPool = cardPool;
        ReactionDepth = reactionDepth;
    }

    /// <summary>The situation the game is in now. Read it where it is needed: the round moves on.</summary>
    public static GameSituation Live
    {
        get
        {
            CardPlayRound round = CardPlayRound.Current;
            return new GameSituation(BoardState.Live, round?.CurrentReactionTrigger, round?.CurrentBlockTrigger,
                                     round?.ChangeEventsPool ?? NoEvents, round?.CardPool ?? NoCards, round?.ReactionDepth ?? 0);
        }
    }

    /// <summary>The same moment on a different board.</summary>
    public GameSituation WithBoard(BoardState board) =>
        new(board, ReactionTrigger, BlockTrigger, Pool, CardPool, ReactionDepth);

    /// <summary>
    /// The after-reaction window of <paramref name="trigger"/>, which has just applied: it is the
    /// trigger and joins the pool. The window is reached from inside a card's activation, so the depth
    /// is at least one.
    /// </summary>
    public GameSituation ReactingTo(ChangeEvent trigger) =>
        new(Board, trigger, null, Pool.Append(trigger).ToList(), CardPool, System.Math.Max(ReactionDepth, 1));

    /// <summary>The same moment once <paramref name="applied"/> has happened: it joins the pool, the trigger stays.</summary>
    public GameSituation After(ChangeEvent applied) =>
        new(Board, ReactionTrigger, BlockTrigger, Pool.Append(applied).ToList(), CardPool, ReactionDepth);

    public T TriggerAs<T>() where T : ChangeEvent => ReactionTrigger as T;

    public List<T> PoolEvents<T>() where T : ChangeEvent => Pool.OfType<T>().ToList();
}
