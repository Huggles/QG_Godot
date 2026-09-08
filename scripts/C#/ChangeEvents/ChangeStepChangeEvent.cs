using System.Threading.Tasks;

public partial class ChangeStepChangeEvent : ChangeEvent
{
    public TurnStep NewStep { get; set; }

    public ChangeStepChangeEvent(TurnStep newStep) : base(Faction.NONE)
    {
        NewStep = newStep;
    }

    public override ChangeEventDto ToDto()
    {
        ChangeStepChangeEventDto dto = ChangeEventDto.Build<ChangeStepChangeEventDto>(this, Id);
        dto.NewStep = NewStep;
        return dto;
    }

    protected override Task<bool> ExecuteAsync()
    {
        var flow = GameFlow.Instance;
        flow.TurnStep = NewStep;
        flow.CardPlayRounds.Add(CardPlayRound.StartNew());
        return Task.FromResult(true);
    }

    /// <summary>
    /// Moves the turn step, and opens a fresh CardPlayRound.
    ///
    /// Flow because card conditions read GameFlow.TurnStep. Decks because a new round means an empty
    /// CardPool, and CalculatePlayedCardsForFaction counts pool cards as played. NOT Board: no piece
    /// moves, no country changes hands, no strait flips — which matters, because this is the single
    /// most frequent event in a game (over half of them) and it was re-deriving supply, attackability
    /// and buildability for all six factions each time.
    /// </summary>
    public override RecalcScope RecalcScope => RecalcScope.Decks | RecalcScope.Flow;

    /// <summary>
    /// Kept out of the history strip. A step change fires several times a round and belongs to no
    /// faction, so it produced a run of flagless grey badges that pushed the entries a player actually
    /// wants — who deployed, who drew, who battled — out of the capped window.
    /// </summary>
    public override bool ToHistoryItem => false;

    public override string SummaryText() => $"Turn step changed to {NewStep}";
}
