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

    protected override async Task<bool> ExecuteAsync()
    {
        if (!GameFlow.Instance.CardsPlayedThisTurnStep.ContainsKey(TriggeringFaction))
            GameFlow.Instance.CardsPlayedThisTurnStep[TriggeringFaction] = 1;
        else
            GameFlow.Instance.CardsPlayedThisTurnStep[TriggeringFaction] += 1;
        await Task.CompletedTask;
        return true;
    }

    public override string SummaryText() => $"{TriggeringFaction.WithPlayer()} spent their play action";
}
