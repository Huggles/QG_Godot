/// <summary>When a step pays its prices (<see cref="StepPrice"/>).</summary>
public enum PriceTiming
{
    /// <summary>Before the step does anything, as the separate cost step it replaces did. Nothing can cancel the step first.</summary>
    BeforeBody,

    /// <summary>
    /// A ResultStep: after its target is chosen, inside dispatch, after the effect event's block window.
    /// A card cancelled in that window never pays (CardPlayRound.DoChangeEvent).
    /// </summary>
    InDispatch,
}
