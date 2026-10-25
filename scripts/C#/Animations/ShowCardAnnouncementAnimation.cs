using System.Threading.Tasks;

/// <summary>Pops a played, activated or used card in beside the history strip, then settles it into the trigger slot until the next prompt.</summary>
public class ShowCardAnnouncementAnimation : ChangeEventAnimation
{
    private readonly int _cardId;
    private readonly string _title;
    private readonly bool _toTable;

    /// <param name="toTable">The card is going onto the table, so it leaves the slot into its faction's Active Cards button.</param>
    public ShowCardAnnouncementAnimation(int cardId, string title, bool toTable = false)
    {
        _cardId = cardId;
        _title = title;
        _toTable = toTable;
    }

    protected override Task AnimateForTargetFaction()
        => TriggerContextDisplay.Current?.AnnounceCard(_cardId, _title, _toTable) ?? Task.CompletedTask;
}
