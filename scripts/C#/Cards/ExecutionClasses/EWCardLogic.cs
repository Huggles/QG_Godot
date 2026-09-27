using Godot;
using System;

public abstract partial class EWCardLogic : CardLogic
{
    /// <summary>
    /// The VP the previous (scoring) step awarded, which the discard step scales from; 0 when it scored
    /// nothing. Read off that step's event rather than recounted, so a reaction moving the board between
    /// the two steps cannot make the discard disagree with the VP. Throws on an unknown previous step.
    /// </summary>
    protected static int ScoredBy(StepOption? previous)
        => (previous.Value.Event as ScorePointsChangeEvent)?.VPTurnSummary.TotalScore ?? 0;
}
