using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// A scripted overlay on the turn loop, installed with <see cref="GameFlow.InstallProgram"/>.
///
/// Every method here is a seam <see cref="GameFlow"/> yields at, not a replacement for what GameFlow
/// does. The real rules engine — CountryState, UnitState, CardState, ChangeEvents, CardPlayRounds,
/// reactions — runs underneath unchanged, which is the point: a scripted game's board is genuinely
/// correct at all times, so the same cards, the same conditions and the same reaction windows apply.
///
/// Host-only. Every GameFlow seam that consults a program is already behind Multiplayer.IsServer(),
/// and the one thing that uses this — tutorial mode — is single player, so the host is the only peer
/// there is. Nothing here needs to reach a client, and nothing here does.
///
/// Every member has a default, so an implementation declares only the seams it actually uses, and a
/// new seam added here costs existing programs nothing.
/// </summary>
public interface ITurnProgram
{
    /// <summary>Called once by <see cref="GameFlow.InstallProgram"/>, before the game starts.</summary>
    void Attach(GameFlow flow);

    /// <summary>
    /// This program's step table, or null to keep GameFlow's own seven steps — which is what every
    /// program that only brackets the normal loop should return.
    ///
    /// A program that DOES supply its own table owns two consequences: the save snapshot's
    /// TurnStepCounter is meaningless under a different table (see <see cref="AllowsSaving"/>), and
    /// a peer other than the host would index its own table for the NextStepStarted emit.
    /// </summary>
    List<GameFlow.GameTurnStep> BuildTurnSteps(GameFlow flow) => null;

    /// <summary>The opening deal is done and the first turn is about to start.</summary>
    Task OnGameStarted() => Task.CompletedTask;

    /// <summary>A new turn has begun: the round event has applied and the counter is reset.</summary>
    Task OnTurnStarted(int gameTurn, Faction faction) => Task.CompletedTask;

    /// <summary>
    /// A turn step has opened — its ChangeStepChangeEvent has applied and its CardPlayRound exists —
    /// and its BEFORE mutators have NOT yet run. Awaited, so the step waits for whatever this does.
    /// </summary>
    Task BeforeStep(TurnStep step, Faction faction) => Task.CompletedTask;

    /// <summary>
    /// A turn step has fully resolved and its AFTER mutators have run. Awaited, and it runs before
    /// the loop advances, so narration here lands inside the step it describes.
    /// </summary>
    Task AfterStep(TurnStep step, Faction faction) => Task.CompletedTask;

    /// <summary>
    /// False refuses every save with <see cref="SaveBlockedReason"/> as the explanation. A scripted
    /// program's own position is not in the GameFlow snapshot, so a restore would rebuild the board
    /// and lose the script.
    /// </summary>
    bool AllowsSaving => true;

    /// <summary>Shown to the player when <see cref="AllowsSaving"/> is false.</summary>
    string SaveBlockedReason => "this game mode cannot be saved";

    /// <summary>
    /// The turn loop is being resumed after a reported failure, with the rest of the failed step
    /// abandoned. A scripted program has lost its place by definition — whatever the failed step was
    /// going to consume is still queued — so the honest response is usually to stand down rather than
    /// to guess.
    /// </summary>
    void OnResumeAfterFailure() { }
}
