using System.Threading.Tasks;

/// <summary>
/// Holds the queue until a presentation the event already started has finished. Lets an event start
/// its visuals before its broadcast but wait on them after it, so clients are not held back.
/// </summary>
public class AwaitTaskAnimation : ChangeEventAnimation
{
    private readonly Task _task;

    public AwaitTaskAnimation(Task task)
    {
        _task = task;
    }

    protected override Task AnimateForTargetFaction() => _task;
    protected override Task AnimateForEnemyFaction() => _task;
}
