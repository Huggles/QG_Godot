using System.Collections.Generic;

/// <summary>
/// A scenario mutator the player chooses to activate, rather than one that always runs at a step
/// boundary (see IStepMutator / StepMutatorRunner for those). It declares triggers and an OnActivate
/// effect exactly like a Status card, and is presented to the player as a Bulletin — a card in every
/// respect the UI cares about (see BulletinCardState).
///
/// It is NOT a card: it has no entry in QGData_Cards_V2.json, it is never in a deck, and it is never
/// discarded. It is declared in a scenario's "mutators" array and registered by
/// GameModeMultiplayerDefault.RegisterMutators, which builds one BulletinCardState per eligible
/// faction and hands it a fresh instance of this class as its CardLogic.
///
/// Extending CardLogic is the reuse seam: it makes the whole existing activation pipeline —
/// Tag.IsActivatable, Tag.IsAfterReaction, CardPlayRound.RequestAfterReactions, CardStep,
/// ActivateReactionChangeEvent, the block/after-reaction windows and the card UI — apply with no
/// changes. Because each eligible faction gets its own instance, CardLogic.Faction is always the
/// faction that may activate it, so trigger and effect code can use Faction directly.
/// </summary>
public abstract partial class ActivatableMutator : CardLogic
{
    /// <summary>Card title shown in the UI. Baked into the synthetic CardData at registration.</summary>
    public abstract string Label { get; }

    /// <summary>
    /// Card body text shown in the UI. Read twice: once at registration, to bake into the synthetic
    /// CardData (which is what the wire and any consumer other than CardFace sees), and again on every
    /// render through BulletinCardState.DisplayText. So an override MAY compute from live game state —
    /// but only from state every peer shares, or two players would read a different card.
    ///
    /// Both reads happen with CardState already assigned, which is what makes <see cref="CardLogic.Faction"/>
    /// safe to use here. RegisterBulletinCardChangeEvent depends on that ordering: it builds the
    /// CardData without Text, wires CardState, and only then fills Text in. Reading Text any earlier
    /// dereferences a null CardState.
    /// </summary>
    public abstract string Text { get; }

    /// <summary>
    /// Mirrors CardData.MultipleActivationsPerTurn: false (the default) means once per faction turn.
    /// Read at registration and copied onto the synthetic CardData, which is what CanBeActivated reads.
    /// </summary>
    public virtual bool MultipleActivationsPerTurn => false;

    // Scenario window, assigned from the MutatorScenarioData entry at registration. Mirrors the
    // FromRound/ToRound fields on StepMutator.
    public int FromRound { get; set; } = 1;
    public int ToRound { get; set; } = int.MaxValue;

    /// <summary>
    /// The mutator's own triggers, in the same form as a card's CardTriggers(). At least one should be
    /// event-scoped (an EventCondition, or a CustomCondition marked .InReactionWindow()) or
    /// CardLogic.CanBeActivated will only ever offer this mutator at reaction depth 0.
    /// </summary>
    protected abstract List<Condition> MutatorTriggers();

    /// <summary>
    /// Sealed so a subclass cannot accidentally drop the scenario round window. The round condition is
    /// deliberately NOT marked .InReactionWindow(): HasEventBasedTrigger is an Any(...) over the
    /// conditions, so the event-scoped trigger from MutatorTriggers() still satisfies it.
    /// </summary>
    protected sealed override List<Condition> CardTriggers()
    {
        List<Condition> conditions = new List<Condition>
        {
            Condition.Build(new Condition.CustomCondition(() =>
                GameFlow.Instance.Round >= FromRound && GameFlow.Instance.Round <= ToRound), this)
        };
        conditions.AddRange(MutatorTriggers());
        return conditions;
    }
}
