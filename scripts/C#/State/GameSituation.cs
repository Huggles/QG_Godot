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

    /// <summary>
    /// A later turn projected on a fork: every card's steps count as re-armed, as
    /// <see cref="CardLogic.OnNewTurnStarted"/> would leave them. Step progress lives on the shared
    /// CardStep objects rather than on the board, so a fork cannot reset it itself.
    /// </summary>
    public bool Rearmed { get; }

    private GameSituation(BoardState board, ChangeEvent reactionTrigger, ChangeEvent blockTrigger,
                          IReadOnlyList<ChangeEvent> pool, IReadOnlyList<CardState> cardPool, int reactionDepth,
                          bool rearmed = false)
    {
        Board = board;
        ReactionTrigger = reactionTrigger;
        BlockTrigger = blockTrigger;
        Pool = pool;
        CardPool = cardPool;
        ReactionDepth = reactionDepth;
        Rearmed = rearmed;
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
        new(board, ReactionTrigger, BlockTrigger, Pool, CardPool, ReactionDepth, Rearmed);

    /// <summary>
    /// The start of a later turn on <paramref name="board"/>, a fork already moved to it: no window
    /// open, nothing in the round's pools, every card re-armed. For the bot's lookahead.
    /// </summary>
    public static GameSituation NewTurnOn(BoardState board) =>
        new(board, null, null, NoEvents, NoCards, 0, rearmed: true);

    /// <summary>
    /// The after-reaction window of <paramref name="trigger"/>, which has just applied: it is the
    /// trigger and joins the pool. The window is reached from inside a card's activation, so the depth
    /// is at least one.
    /// </summary>
    public GameSituation ReactingTo(ChangeEvent trigger) =>
        new(Board, trigger, null, Pool.Append(trigger).ToList(), CardPool, System.Math.Max(ReactionDepth, 1), Rearmed);

    /// <summary>
    /// The block window for <paramref name="trigger"/>, which has not applied yet: the event a block
    /// card's triggers read, as CardPlayRound.RequestBlockReactions offers it. For the bot's forecast.
    /// </summary>
    public GameSituation OfferingForBlock(ChangeEvent trigger) =>
        new(Board, ReactionTrigger, trigger, Pool, CardPool, ReactionDepth, Rearmed);

    /// <summary>The same moment once <paramref name="applied"/> has happened: it joins the pool, the trigger stays.</summary>
    public GameSituation After(ChangeEvent applied) =>
        new(Board, ReactionTrigger, BlockTrigger, Pool.Append(applied).ToList(), CardPool, ReactionDepth, Rearmed);

    public T TriggerAs<T>() where T : ChangeEvent => ReactionTrigger as T;

    public List<T> PoolEvents<T>() where T : ChangeEvent => Pool.OfType<T>().ToList();
}
