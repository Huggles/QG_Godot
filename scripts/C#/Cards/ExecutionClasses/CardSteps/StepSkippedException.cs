using System;

/// <summary>
/// Control-flow signal thrown on the requesting peer when a player skips a card step's
/// selection input. Thrown by <see cref="InputRequest.BroadCast"/> when the response
/// DTO has <see cref="InputRequest.WasSkipped"/> set, and caught centrally in
/// <see cref="CardStep.Execute"/> so no per-card execution class needs to handle skip.
/// </summary>
public class StepSkippedException : Exception
{
    public StepSkippedException() : base("Player skipped the card step selection.") { }
}
