using System.Threading.Tasks;

/// <summary>Pops a played, activated or used card in beside the history strip, then settles it into the trigger slot until the next prompt.</summary>
public class ShowCardAnnouncementAnimation : ChangeEventAnimation
{
    private readonly int _cardId;
    private readonly string _title;

    public ShowCardAnnouncementAnimation(int cardId, string title)
    {
        _cardId = cardId;
        _title = title;
    }

    protected override Task AnimateForTargetFaction()
        => TriggerContextDisplay.Current?.AnnounceCard(_cardId, _title) ?? Task.CompletedTask;
}
