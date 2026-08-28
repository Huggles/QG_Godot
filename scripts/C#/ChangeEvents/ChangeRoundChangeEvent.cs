using System.Threading.Tasks;

public partial class ChangeRoundChangeEvent : ChangeEvent
{
    public int NewTurn { get; set; }

    public ChangeRoundChangeEvent(int newTurn) : base(Faction.NONE)
    {
        NewTurn = newTurn;
    }

    public override ChangeEventDto ToDto()
    {
        ChangeRoundChangeEventDto dto = ChangeEventDto.Build<ChangeRoundChangeEventDto>(this, Id);
        dto.NewTurn = NewTurn;
        return dto;
    }

    protected override async Task<bool> ExecuteAsync()
    {
        GameFlow.Instance.GameTurn = NewTurn;
        GameFlow.Instance.CardsPlayedThisTurnStep.Clear();
        // After GameTurn moved, so CurrentFaction is the faction whose turn is starting.
        GameFlow.Instance.DropOwnTurnReactionSkip();

        ResetPerTurnState();

        await Task.CompletedTask;
        return true;
    }

    /// <summary>
    /// Clear the state that only lasts a turn.
    ///
    /// This lives in the event rather than on the EventBus NewTurnStarted signal, which is where it used
    /// to be, for two reasons. That signal is emitted from GameFlow.StartNewTurn, which is host-only, so
    /// a client never cleared any of it — and ImmuneForTurn and SuppliedForTurn are both covered by
    /// MultiplayerGameState.ComputeHash, so the two peers drifted apart the first time a unit was
    /// supplied and the turn rolled over. And a save-game restore replays events without running the
    /// turn loop, so a signal-driven reset never fired there either: every unit ever supplied would come
    /// back supplied, and every status card would come back with last turn.s steps still spent.
    ///
    /// The rule this is an instance of: authoritative state must change as a consequence of a
    /// ChangeEvent, never as a consequence of a local signal.
    /// </summary>
    private void ResetPerTurnState()
    {
        MultiplayerGameState gameState = MultiplayerSession.Instance?.GameState;
        if (gameState == null) return;

        foreach (UnitState unitState in gameState.UnitStates)
        {
            unitState.ImmuneForTurn   = false;
            unitState.SuppliedForTurn = false;
        }

        // Re-arms a status card.s steps for the new turn, and drops any activation binding an aborted
        // play left behind. See CardLogic.OnNewTurnStarted.
        foreach (CardState cardState in gameState.CardStates)
            cardState.CardLogic?.OnNewTurnStarted(NewTurn);
    }

    public override bool ToHistoryItem => true;

    public override string SummaryText() => $"Game turn advanced to {NewTurn}";
}
