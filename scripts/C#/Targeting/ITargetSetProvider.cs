/// <summary>
/// Implemented by anything that can say what it would affect without doing it — today every
/// <see cref="CardLogic"/>, which covers cards and, because <c>ActivatableMutator</c> IS a CardLogic,
/// the Bulletins a scenario mutator is drawn as.
///
/// Consulted on the HOST by <c>InputRequest.PopulateCardTargetPreviews</c>, which turns the answer
/// into <c>InputRequest.CardTargetPreviews</c> for the hover preview that lights up a card's targets
/// while the player is still choosing a card. Read through
/// <see cref="ITargetSetProviderExtensions.TargetsOrNone"/>, never called bare.
///
/// Deliberately NOT an <c>IModifier</c>: that marker means "registered with ModifierRegistry and
/// active while its card is played", which has nothing to do with being able to describe your
/// targets. <c>ITaggable</c> and <c>IGameEventHandler</c> are the precedent for a base-less
/// capability interface here.
///
/// Not yet implemented by <c>IStepMutator</c>: those four mutators do their work in
/// <c>Task Run(Faction)</c> rather than in CardSteps, are not CardStates, and — the point — are never
/// drawn anywhere a player could hover them, so the member would have no consumer. When one exists,
/// IStepMutator implements this; its existing default-interface-member idiom
/// (<c>BulletinText => string.Empty</c>, re-declared virtual on StepMutator because a subclass cannot
/// override a default interface member) is the template.
/// </summary>
public interface ITargetSetProvider
{
    /// <summary>
    /// What this would target if used right now. Return <see cref="TargetSet.None"/> to declare
    /// nothing — the neutral answer, and the correct one for anything whose targets cannot be known
    /// before it runs.
    /// </summary>
    TargetSet Targets();
}
