using System.Threading.Tasks;

/// <summary>Pops a played, activated or used card in beside the history strip, then settles it into the trigger slot until the next prompt.</summary>
public class ShowCardAnnouncementAnimation : ChangeEventAnimation
{
    private readonly int _cardId;
    private readonly string _title;
    private readonly bool _intoHistory;

    /// <param name="intoHistory">Zoom on into the history strip instead of resting in the trigger slot.</param>
    public ShowCardAnnouncementAnimation(int cardId, string title, bool intoHistory = false)
    {
        _cardId = cardId;
        _title = title;
        _intoHistory = intoHistory;
    }

    protected override Task AnimateForTargetFaction()
        => TriggerContextDisplay.Current?.AnnounceCard(_cardId, _title, _intoHistory) ?? Task.CompletedTask;
}
