using System.Threading.Tasks;

/// <summary>
/// Shared helper for emitting a mutator's change events. Lives on the interface rather than on
/// StepMutator so that card logic classes implementing IStepMutator — which cannot inherit
/// StepMutator — get the same entry point.
/// </summary>
public static class StepMutatorExtensions
{
    /// <summary>
    /// Push a change event through the full reaction pipeline, so other factions can block it and
    /// respond to it exactly as they would to a card's step.
    ///
    /// isTrigger:false applies the event directly instead — for bookkeeping moves that should not
    /// open a block or reaction window.
    /// </summary>
    public static async Task Do(this IStepMutator mutator, ChangeEvent changeEvent, bool isTrigger = true)
    {
        changeEvent.IsTrigger = isTrigger;
        if (isTrigger)
            await CardPlayPool.DoChangeEvent(changeEvent);
        else
            await changeEvent.Apply();
    }
}
