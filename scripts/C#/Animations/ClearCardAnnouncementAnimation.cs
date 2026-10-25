using System.Threading.Tasks;

/// <summary>Takes the announced card out of the trigger slot. Queued rather than called, so it runs after a flight still pending.</summary>
public class ClearCardAnnouncementAnimation : ChangeEventAnimation
{
    protected override Task AnimateForTargetFaction()
    {
        TriggerContextDisplay.Current?.ClearAnnouncement();
        return Task.CompletedTask;
    }
}
