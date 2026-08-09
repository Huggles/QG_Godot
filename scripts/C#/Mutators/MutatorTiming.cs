/// <summary>
/// When a mutator runs relative to its turn step. BEFORE fires once the step's
/// ChangeStepChangeEvent has applied; AFTER fires once the step's handler has completed,
/// before the turn loop advances.
/// </summary>
public enum MutatorTiming
{
    BEFORE, AFTER
}
