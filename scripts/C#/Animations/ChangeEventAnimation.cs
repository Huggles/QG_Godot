using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Base class for animations that play before or after a ChangeEvent is applied.
/// Override AnimateBefore / AnimateAfter in a ChangeEvent subclass to return instances.
/// </summary>
public abstract class ChangeEventAnimation
{
    public abstract Task Execute();
}
