using System.Threading.Tasks;

/// <summary>
/// A mutator runs immediately before or after a turn step and behaves like a card activation:
/// it may emit ChangeEvents (blockable, reactable) and request player input through InputRequest.
/// The normal turn flow around it stays intact.
///
/// Registered with ModifierRegistry — by DeckState.PlayCard for status-card mutators, by the
/// scenario at setup, or explicitly by a card step that wants to schedule an effect (see
/// MutatorRecycleAfterStep).
///
/// TurnStep.END is not a hook point — it is the turn rollover. Express "end of turn" as AFTER DRAW.
/// </summary>
public interface IStepMutator : IModifier
{
    TurnStep      Step   { get; }
    MutatorTiming Timing { get; }

    /// <summary>
    /// Shown to all players as the title of this mutator's Bulletin card when it fires, and as the
    /// action label beside any input it asks for. Keep it a readable sentence — StepMutator defaults it
    /// to the class name, which reads badly on a card.
    /// </summary>
    string Description { get; }

    /// <summary>Body text of the Bulletin card. Empty hides the card's text box entirely.</summary>
    string BulletinText => string.Empty;

    Task Run(Faction activeFaction);

    /// <summary>Lower runs first within a window; ties fall back to registration order.</summary>
    int Order => 0;

    /// <summary>Checked at every window; true unregisters this mutator permanently.</summary>
    bool IsExpired => false;

    bool ShouldRun(Faction activeFaction) => true;
}
