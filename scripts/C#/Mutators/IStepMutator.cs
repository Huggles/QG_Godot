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

    /// <summary>
    /// Body text of the Bulletin card. Empty hides the card's text box entirely. Unused when
    /// <see cref="SourceCardId"/> names a card — that card's own face is shown instead of a Bulletin.
    /// </summary>
    string BulletinText => string.Empty;

    /// <summary>
    /// The card that put this mutator in play, or -1 when the scenario declared it. This is the only
    /// record of a mutator's origin — the registry holds no per-entry metadata, and the three
    /// registration sites are otherwise indistinguishable once they have run.
    ///
    /// It decides how the mutator announces itself when it fires: a card-sourced mutator shows that
    /// card, exactly as a card activation does, while a scenario one shows a Bulletin built from
    /// <see cref="Description"/> and <see cref="BulletinText"/>.
    ///
    /// A card logic class that IS the mutator fills this in with a one-liner, since CardLogic already
    /// holds the state: <c>public int SourceCardId =&gt; CardState.Id;</c>. A detached mutator a card
    /// step registers takes the id through its constructor — see MutatorRecycleAfterStep.
    /// </summary>
    int SourceCardId => -1;

    Task Run(Faction activeFaction);

    /// <summary>Lower runs first within a window; ties fall back to registration order.</summary>
    int Order => 0;

    /// <summary>Checked at every window; true unregisters this mutator permanently.</summary>
    bool IsExpired => false;

    bool ShouldRun(Faction activeFaction) => true;
}
