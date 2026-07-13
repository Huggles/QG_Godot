using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Base class for animations that play before or after a ChangeEvent is applied.
/// Override BeforeAnimations / AfterAnimations in a ChangeEvent subclass to return instances.
/// </summary>
public abstract class ChangeEventAnimation
{
    private readonly TaskCompletionSource _tcs = new();

    /// <summary>Awaitable that completes when the queue finishes executing this animation.</summary>
    public Task CompletionTask => _tcs.Task;
    public string ScriptName => GetType().ToString();

    /// <summary>Called by AnimationQueue after Execute() finishes (or immediately if BlockQueue is false).</summary>
    internal void Complete() => _tcs.SetResult();

    /// <summary>
    /// When true (default), the queue waits for this animation to finish before starting the next one.
    /// When false, the queue fires this animation and immediately starts the next one.
    /// </summary>
    public virtual bool BlockQueue { get; set; } = true;

    public virtual List<Faction> ForFactions => StaticGameData.PlayableFactions;
    public bool ForMyFaction => PlayerScene.Current.ControlledFactions.Intersect(ForFactions).Any();

    public async Task Execute()
    {
        if(ForMyFaction)
        {
            await AnimateForTargetFaction();
        }
        else
        {
            await AnimateForEnemyFaction();
        }
    }

    protected abstract Task AnimateForTargetFaction();
    protected virtual Task AnimateForEnemyFaction(){ return Task.CompletedTask; }
}
