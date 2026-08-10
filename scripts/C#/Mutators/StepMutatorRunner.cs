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
    /// <summary>
    /// The Bulletin of the mutator currently executing, or null outside a mutator's Run(). Read by
    /// InputRequest.BroadCast so any input a mutator asks for carries its Bulletin to the player, which
    /// is what makes "why am I being asked to pick a unit?" answerable. Ambient rather than threaded
    /// through, mirroring CardPlayRound.CurrentReactionTrigger; server-side only, like the runner.
    /// </summary>
    public static (string Label, string Text)? RunningBulletin { get; private set; }

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

                // Announce the mutator as a Bulletin card on every peer. Replaces the old
                // ShowPlayerActionLabel RPC, which was a single line of text that nothing cleared and
                // that any client-side input round-trip wiped (see NetworkApi.ReceiveInputResponse).
                //
                // A PresentationEvent, so it costs no state hash, no tag recalculation and no reach into
                // the reaction chain — it only needs its position in the replicated stream, so that the
                // modal lands with the effects it announces rather than racing them.
                await new ShowBulletinPresentationEvent(activeFaction, mutator.Description, mutator.BulletinText).Apply();

                // Load-bearing, and it used to be redundant: the announcement was a ChangeEvent, so its
                // own Apply() recalculated too and this was the second pass of two. As a PresentationEvent
                // it recalculates nothing, so this is now the only thing guaranteeing scenario code reads
                // fresh tags — and mutators do read them. RemoveUnitChangeEvent captures WasInSupply from
                // the tag-derived UnitState.InSupply in its constructor, which MutatorRemoveUnitAfterPlayCard
                // builds inside Run() below.
                GameStateCalculator.CalculateAll();

                RunningBulletin = (mutator.Description, mutator.BulletinText);
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
                finally
                {
                    // Must clear on skip and on any throw too, or the next unrelated input request
                    // would show a stale Bulletin beside it.
                    RunningBulletin = null;
                }
            }
        }
        finally
        {
            // Belt to the per-mutator finally's braces: the stale-epoch check at the top of the loop
            // throws past it, and nothing outside this runner should ever observe a Bulletin as running.
            RunningBulletin = null;
            if (createdRound) CardPlayRound.Current?.ClearPool();
        }
    }
}
