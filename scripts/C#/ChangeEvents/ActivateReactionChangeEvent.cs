using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class ActivateReactionChangeEvent : ChangeEvent
{
    private ChangeEvent SourceChangeEvent;
    protected int StepId;

    /// <summary>
    /// True when this activation opens the card's first step. A multi-step card is resumed by
    /// CardPlayRound.ContinueWithNextSteps -> DoCard, which emits a fresh ActivateReactionChangeEvent for
    /// every step; announcing the card on each of them would re-show the same modal between steps.
    ///
    /// Computed from CardStep.StepFinished on the server and replicated: a client never runs card steps,
    /// so every activation would look like a first step there. Status cards reset their steps in
    /// CardLogic.OnNewTurnStarted, so this is first-step-per-turn, matching when the steps rerun.
    /// </summary>
    public bool IsFirstStep { get; set; } = true;

    public ActivateReactionChangeEvent(Faction faction, int cardId, ChangeEvent sourceChangeEvent) : base(faction)
    {
        this.SourceChangeEvent = sourceChangeEvent;
        this.SourceCardId = cardId;
        this.StepId = SourceCardState.CardLogic.CardSteps[0].Id;
        // Captured in the constructor: DoCard builds this event before executing the step that flips
        // StepFinished, so at this point "nothing finished yet" means "this is the first step".
        this.IsFirstStep = SourceCardState.CardLogic.CardSteps.All(step => !step.StepFinished);
    }

    public override ChangeEventDto ToDto()
    {
        ActivateReactionChangeEventDto dto = ChangeEventDto.Build<ActivateReactionChangeEventDto>(this, Id);
        dto.IsFirstStep = IsFirstStep;
        return dto;
    }

    protected override void ApplyDtoFields(GameMessageDto dto)
    {
        base.ApplyDtoFields(dto);
        if (dto is ActivateReactionChangeEventDto d)
            IsFirstStep = d.IsFirstStep;
    }

    /// <summary>Announce the activated card to every player — but only once per card, on its first step.</summary>
    protected override List<ChangeEventAnimation> AfterAnimations => IsFirstStep
        ? new()
        {
            new ShowCardsModalAnimation(
                new List<int> { SourceCardId },
                $"{TriggeringFaction.WithPlayer()} activates {SourceCardState.CardName}")
        }
        : new();

    protected override async Task<bool> ExecuteAsync(){
        DebugUtilities.PrintPeer($"Activating reaction card {SourceCardState.CardName} for faction {TriggeringFaction}");
        SourceCardState.ActivatedInTurns.Add(GameFlow.Instance.GameTurn);

        // Flip the card face up for every peer. Set here rather than in AfterAnimations so it is
        // already true when the "X activates Y" modal builds its CardScene: ChangeEvent.ApplyMutation
        // awaits ExecuteAsync before it enqueues AfterAnimations, on the server and on each client
        // replaying the event off ChangeEventQueue alike. Idempotent — a multi-step Response card
        // emits a second ActivateReactionChangeEvent via CardPlayRound.ContinueWithNextSteps.
        SourceCardState.IsRevealed = true;

        // A Response card is spent once activated and moves to the discard pile. Status cards stay on
        // the table (and stay registered as modifiers), so they are deliberately excluded.
        //
        // The IsDiscarded guard is required, not defensive: multi-step Response cards are resumed by
        // CardPlayRound.ContinueWithNextSteps -> DoCard, which emits a SECOND ActivateReactionChangeEvent
        // for the same card. DeckState.DiscardCard falls through all its branches for an id already in
        // DiscardedCardIds and appends a duplicate, which would corrupt the pile and ComputeHash.
        if (SourceCardState.CardData.CardType == CardType.RESPONSE && !SourceCardState.IsDiscarded)
            DeckState.ForFaction(TriggeringFaction).DiscardCard(SourceCardId);

        return true;
    }

    public override string SummaryText() => $"{TriggeringFaction.WithPlayer()} activated card {SourceCardState.CardName}";
}
