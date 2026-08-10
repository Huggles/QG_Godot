using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Base for standalone mutators — those declared by a scenario and those a card step registers as a
/// detached instance. Holds the scenario JSON filters (faction / round range / order).
///
/// Step and Timing are abstract properties rather than fields so a subclass can either hard-code them
/// or set them from its constructor; MutatorRecycleAfterStep does the latter.
///
/// Card logic classes that ARE the mutator implement IStepMutator directly instead — they already
/// extend CardLogic and cannot inherit this.
/// </summary>
public abstract class StepMutator : IStepMutator
{
    public abstract TurnStep      Step   { get; }
    public abstract MutatorTiming Timing { get; }

    public virtual string Description => GetType().Name;

    // Re-declared here rather than inherited as IStepMutator's default: a subclass cannot override a
    // default interface member, and every scenario mutator wants to fill this in. Same shape as
    // Description above.
    public virtual string BulletinText => string.Empty;

    // Populated from the scenario entry at registration time; left at defaults for detached mutators.
    public List<Faction> FactionFilter { get; set; } = new();
    public int FromRound { get; set; } = 1;
    public int ToRound   { get; set; } = int.MaxValue;
    public int Order     { get; set; } = 0;

    public virtual bool IsExpired => GameFlow.Instance.Round > ToRound;

    public virtual bool ShouldRun(Faction activeFaction) =>
        (FactionFilter.Count == 0 || FactionFilter.Contains(activeFaction))
        && GameFlow.Instance.Round >= FromRound
        && GameFlow.Instance.Round <= ToRound;

    public abstract Task Run(Faction activeFaction);
}
