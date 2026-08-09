using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// The single execution point for step mutators. Called from GameFlow at two seams: BeginStep (after
/// the step's ChangeStepChangeEvent applies) and FinishStep (once the step's handler completes).
///
/// Server-side only in practice — GameFlow gates its step handlers on Multiplayer.IsServer(), and
/// mutator effects reach clients as replicated ChangeEvents like everything else.
/// </summary>
public static class StepMutatorRunner
{
    public static async Task Run(TurnStep step, MutatorTiming timing, Faction activeFaction)
    {
        // Retire finished mutators first. ToList() before unregistering: GetAll is a lazy OfType
        // over the registry's backing list, so mutating it mid-enumeration would throw.
        foreach (IStepMutator expired in ModifierRegistry.GetAll<IStepMutator>().Where(m => m.IsExpired).ToList())
        {
            DebugUtilities.PrintPeer($"Mutator {expired.GetType().Name} expired — unregistering");
            ModifierRegistry.Unregister(expired);
        }

        // OrderBy is a stable sort, so an equal Order keeps registration order: scenario mutators
        // (registered during setup) ahead of card mutators, each group in the order it was added.
        List<IStepMutator> due = ModifierRegistry.GetAll<IStepMutator>()
            .Where(m => m.Step == step && m.Timing == timing && m.ShouldRun(activeFaction))
            .OrderBy(m => m.Order)
            .ToList();

        if (due.Count == 0) return;

        int epoch = ErrorReporter.GameLoopEpoch;

        // AFTER hooks on START / PLAY_CARD arrive once CardPlayRound.Finish() has already nulled
        // Current, so give the mutators their own round to register events into. Tear it down with
        // ClearPool(), never Finish() — Finish() emits the global CardPlayPoolFinished signal that the
        // play-step handlers listen on, which would advance the turn loop a second time.
        bool createdRound = CardPlayRound.Current == null;
        if (createdRound) GameFlow.Instance.CardPlayRounds.Add(CardPlayRound.StartNew());

        try
        {
            foreach (IStepMutator mutator in due)
            {
                ErrorReporter.ThrowIfStaleEpoch(epoch);
                DebugUtilities.PrintPeer($"Mutator {mutator.GetType().Name} ({timing} {step})");
                NetworkApi.Instance?.Rpc(nameof(NetworkApi.ShowPlayerActionLabel),
                    mutator.Description, -1, (int)activeFaction);
                GameStateCalculator.CalculateAll();

                try
                {
                    await mutator.Run(activeFaction);
                }
                catch (StepSkippedException)
                {
                    // The player declined this mutator's selection. Only this mutator is abandoned;
                    // the rest of the window still runs, and the turn loop continues.
                    DebugUtilities.PrintPeer($"Mutator {mutator.GetType().Name} skipped by player");
                }
            }
        }
        finally
        {
            if (createdRound) CardPlayRound.Current?.ClearPool();
        }
    }
}
