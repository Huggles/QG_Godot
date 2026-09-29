using System.Threading.Tasks;

/// <summary>
/// Marks that a faction has consumed their play action for the current turn step.
/// Used by Status cards (e.g. Bravado) that activate "instead of playing from hand"
/// so the play action is consumed in a network-safe way without a PlayCardChangeEvent.
/// </summary>
public partial class SpendPlayActionChangeEvent : ChangeEvent
{
    public SpendPlayActionChangeEvent(Faction faction) : base(faction) { }

    public override ChangeEventDto ToDto() =>
        ChangeEventDto.Build<SpendPlayActionChangeEventDto>(this, Id);

    /// <summary>Spends the turn's play, which the board does not model.</summary>
    public override void Project(BoardProjection projection) { }

    public override void Mutate(BoardState board) => board.CountPlay(TriggeringFaction);

    /// <summary>
    /// Increments the per-step play counter and nothing else. Card conditions read that counter, so
    /// step executability and card availability can shift; the board and the piles cannot.
    /// </summary>
    public override RecalcScope RecalcScope => RecalcScope.Flow;

    /// <summary>
    /// Kept out of the history strip.
    /// </summary>
    public override bool ToHistoryItem => false;
}
