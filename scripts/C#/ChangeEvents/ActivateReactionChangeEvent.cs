using Godot;
using System;
using System.Threading.Tasks;

public partial class ActivateReactionChangeEvent : ChangeEvent
{
    private ChangeEvent SourceChangeEvent;
    protected int StepId;


    public ActivateReactionChangeEvent(Faction faction, int cardId, ChangeEvent sourceChangeEvent) : base(faction)
    {
        this.SourceChangeEvent = sourceChangeEvent;
        this.SourceCardId = cardId;
        this.StepId = SourceCardState.CardLogic.CardSteps[0].Id;
    }

    public override ChangeEventDto ToDto() => ChangeEventDto.Build<ActivateReactionChangeEventDto>(this, Id);

    protected override async Task<bool> ExecuteAsync(){               
        DebugUtilities.PrintPeer($"Activating reaction card {SourceCardState.CardName} for faction {TriggeringFaction}");
        SourceCardState.ActivatedInTurns.Add(GameFlow.Instance.GameTurn);

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

    public override string SummaryText() => $"{TriggeringFaction} activated card {SourceCardState.CardName}";
}
