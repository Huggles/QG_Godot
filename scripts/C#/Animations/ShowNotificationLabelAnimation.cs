using System.Threading.Tasks;

public class ShowNotificationLabelAnimation : ChangeEventAnimation
{
    private readonly string _text;
    private readonly Faction _faction;

    public ShowNotificationLabelAnimation(string text, Faction faction)
    {
        BlockQueue = false; // This animation does not block the queue, allowing other animations to run concurrently
        _text = text;
        _faction = faction;
    }

    protected override async Task AnimateForTargetFaction()
    {
        PlayerActionLabel.ShowText(_text, _faction);
        await Task.Delay(GameSettings.DurationMedium);
    }
}
