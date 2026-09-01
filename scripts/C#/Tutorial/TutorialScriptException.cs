using System;

/// <summary>
/// The tutorial script and the game have come apart in a way that cannot be papered over — a
/// constrain naming something the prompt does not offer, or one that would leave nothing selectable.
///
/// Thrown rather than logged because a constraint that silently fails to apply is worse than a stall:
/// the commander goes on telling the player to do one thing while the board still allows everything,
/// so the lesson is wrong and nothing says so. Raised from TutorialRuntime.ConstrainRequest, which
/// NetworkApi.SendInputRequest calls on the host, so it unwinds through the step's Guard to the error
/// popup. Continue there resumes the loop and stands the tutorial down (see OnResumeAfterFailure).
/// </summary>
public class TutorialScriptException : Exception
{
    public TutorialScriptException(string message) : base(message) { }
}
