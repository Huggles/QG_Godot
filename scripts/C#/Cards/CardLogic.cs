using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;

public abstract partial class CardLogic : GodotObject
{
    public CardState CardState;
    public CardData CardData => CardState.CardData;
    public Faction Faction => CardState.Faction;
    public FactionData FactionData => StaticGameData.FactionDataMap.ContainsKey(Faction) ? StaticGameData.FactionDataMap[Faction] : null;

    // IsPlayed is computed based on the card's tag

    public bool IsActivatedOnce => CardState.ActivatedInTurns.Count > 0;
    public bool IsActivatedThisTurn => CardState.ActivatedInTurns.Contains(GameFlow.Instance.GameTurn);
    public bool IsResponse => CardData.Type == "RESPONSE";
    public bool IsStatus => CardData.Type == "STATUS";
    public bool IsPlayFinished = false;
    public bool IsActivationFinished = false;
    public bool IsBlockReaction => CardTriggers().Any(triggerCondition => triggerCondition is Condition.IsBlockRequest);    
    public bool HasEventBasedTrigger => CardTriggers().Any(c => c.RequiresEventContext);
    public bool HasImmediateTrigger => CardTriggers().Any(c => c is Condition.EventCondition ec && ec.IsImmediate);
    public bool HasExecutableCardSteps => CardSteps.Count == 0 || ExecutableCardSteps.Count > 0;
    public int NextStepId => HasExecutableCardSteps ? ExecutableCardSteps[0].Id : -1;
    [Signal] public delegate void CardFinishedEventHandler();
    
    public List<CardStep> CardSteps = new();
    public abstract List<CardStep> OnActivate();

    /// <summary>
    /// The ChangeEvent this activation is reacting to — the event being offered for block, or the
    /// event that opened the after-reaction window. Bound by <see cref="CardPlayRound.DoCard"/> at
    /// activation and released once the card's steps are done, so step logic resolves against the
    /// event that triggered the card.
    ///
    /// Step logic must use this rather than re-reading CardPlayPool.LastNoneNewCardChangeEvent: that
    /// is a live pool read, and every ChangeEvent.Apply() self-registers into the pool (see
    /// ChangeEvent.ApplyMutation), so a prerequisite event applied inside the step — or a reaction
    /// played in the activation window DoCard opens before the steps run — silently retargets it.
    ///
    /// Null for a card played from hand outside a reaction. Trigger conditions (CardTriggers) run
    /// before activation and so cannot use this; they still read the pool directly.
    /// </summary>
    public ChangeEvent ActivationTrigger { get; set; }
    
    // A Status/Response card still in hand is being PLAYED onto the table, not activated.
    // Its CardSteps are the later activation effect, so their executability must not gate the play.
    public bool IsTableCardInHand => (IsStatus || IsResponse) && !CardState.IsPlayed;

    public bool CanBeActivated()
    {
        // A card in the discard pile is out of play. This cannot be inferred from Tag.IsPlayed, which
        // stays set once a card is discarded (GameStateCalculator.CalculatePlayedCardsForFaction), so
        // without this check a spent Response card would still be offered as a reaction next turn.
        // CanBeActivated feeds Tag.IsActivatable, so this one gate covers plays, after-reactions and
        // block reactions alike.
        if (CardState.IsDiscarded)
            return false;

        // For an unplayed card TriggerConditionsMet resolves to _defaultPlayConditions, which is
        // the correct gate for a play.
        if (IsActivationFinished || !TriggerConditionsMet)
            return false;

        if (IsTableCardInHand)
        {
            // Playing it onto the table is a Play-step action, never a reaction from hand.
            // HasEventBasedTrigger is deliberately not consulted: it reads CardTriggers(), which
            // describes the later activation and is meaningless while the card is in hand.
            return (CardPlayRound.Current?.ReactionDepth ?? 0) == 0;
        }

        if (!HasExecutableCardSteps)
            return false;

        // "Once per turn" cards (MultipleActivationsPerTurn = false) may not activate
        // again in the same faction turn.
        if (!CardData.MultipleActivationsPerTurn && IsActivatedThisTurn)
            return false;

        // Cards whose triggers are purely state-based (no event/block conditions) must not
        // appear as options inside a reaction chain — only at reaction depth 0.
        if (!HasEventBasedTrigger)
        {
            int reactionDepth = CardPlayRound.Current?.ReactionDepth ?? 0;
            return reactionDepth == 0;
        }

        return true;
    }
    
    public bool TriggerConditionsMet => _conditions.All(condition=>condition.MeetCondition());


    protected virtual List<Condition> _defaultPlayConditions => new List<Condition> 
    {
        new Condition.IsGameFlowStep(TurnStep.PLAY_CARD),
        new Condition.IsFactionTurn(Faction),
        new Condition.Not(new Condition.HasPlayedCardThisTurnStep(Faction)),
        new Condition.CardHasNotBeenActivatedThisTurn(CardState)
    };
    

    public List<Condition> _conditions
    {
        get {
            if (!CardState.IsPlayed)
                return _defaultPlayConditions;
            if (CardTriggers().Count > 0)
                return CardTriggers();
            // Played with no custom triggers = passive modifier, never re-activatable
            return new List<Condition> { new Condition.Never() };
        }
    }
        

    protected virtual List<Condition> CardTriggers() => new();

    public List<CardStep> ExecutableCardSteps => CardSteps.Where(step => step.HasTagForAny(Tag.IsExecutable)).ToList();

    public CardLogic()
    {   
        EventBus.Instance.NewTurnStarted += OnNewTurnStarted;
    }    

    public void OnNewTurnStarted(int turnNumber)
    {
        if (IsStatus)
            CardSteps.ForEach(step => step.StepFinished = false);

        // Safety net: DoCard releases the binding when the card's steps finish, but an activation
        // abandoned mid-way (exception, aborted epoch) would otherwise leave it dangling.
        ActivationTrigger = null;
    }

    public virtual string PlayActionGuidance() =>
        $"Play {GetType().Name}";

    public virtual string ActivateActionGuidance() =>
        $"Activate {GetType().Name}";

    public T BuildChangeEvent<T>(T changeEvent) where T : ChangeEvent
    {
        changeEvent.SourceCardId = this.CardState.Id;
        return changeEvent;
    }
}
