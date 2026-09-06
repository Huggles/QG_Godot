using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;

public abstract partial class CardLogic : GodotObject, ITargetSetProvider
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

    /// <summary>
    /// This card activates during its faction's Play step, with the play still unspent — instead of a
    /// card being played from hand, or (Volksturm, Superior Planning, Mobile Force, Defense of the
    /// Motherland) in addition to it, which is how "at the beginning of your turn" is expressed since
    /// the TurnStep.START window was removed. Either way it belongs beside the hand in the prompt.
    /// A structural test on the trigger list, deliberately NOT whether the trigger is currently met:
    /// it decides what the play prompt DISPLAYS beside the hand, and a card that cannot be used right
    /// now — cost unpayable, no legal target, play already spent — must be shown unusable rather than
    /// disappear. Selectability is a separate question, answered by Tag.IsActivatable.
    /// </summary>
    public bool IsPlayStepActivation => CardTriggers().Any(c => c is Condition.IsPlayCardStep);

    /// <summary>
    /// A play-step activation that does NOT consume the faction's play. True for the four cards that
    /// read "at the beginning of your turn" — Volksturm, Superior Planning, Mobile Force, Defense of
    /// the Motherland — which carry Condition.IsPlayCardStep purely for its timing and leave the hand
    /// card still playable (CardPlayRound.Start keeps asking until the play is actually spent).
    /// False for the ones that really are played INSTEAD of a hand card (Conscription, Bravado,
    /// Guards, …), each of which emits SpendPlayActionChangeEvent as its first step.
    ///
    /// Declared rather than inferred, and therefore a SECOND statement of a fact the card's steps
    /// already make: what actually spends the play is the three-line SpendPlayActionChangeEvent
    /// prologue at the top of a play-spending card's first step. The two must agree, and nothing
    /// enforces it — a step body is a Func<Task>, so the only way to read the cost off the card is to
    /// run it, which is far too late for a prompt-time cue. Set this to match the prologue: true when
    /// there is none, false when there is. Getting it wrong is cosmetic (a card gleams that should
    /// not, or does not that should), never a rules bug — the prologue remains the mechanism.
    ///
    /// Drives the emphasis cue in the play prompt — see FactionHandDisplay.LayoutFan. A free
    /// activation costs nothing to use, so missing one is a pure loss, and these four are newly
    /// beside the hand: they used to get a prompt of their own at TurnStep.START.
    /// </summary>
    public virtual bool IsFreePlayStepActivation => false;

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

    /// <summary>
    /// The event this card is reacting to, resolved for whichever reaction window is actually open.
    /// For <see cref="Targets"/>, which runs BEFORE activation and so cannot use ActivationTrigger.
    ///
    /// CardPlayRound keeps two triggers and they never overlap for one event: RequestBlockReactions
    /// sets CurrentBlockTrigger, RequestAfterReactions sets CurrentReactionTrigger, and DoChangeEvent
    /// runs the block window strictly before the after-reaction one. So a block card reading
    /// CurrentReactionTrigger sees nothing at the top of a chain and the ENCLOSING window's event
    /// inside a nested one. IsBlockReaction is the discriminator, and deliberately not a null-coalesce
    /// down the two: ReactionWindowDisplayCardIds puts non-block Status cards into a block window's
    /// display set, and a preview is computed for those too — they must not read the block trigger.
    ///
    /// ActivationTrigger still wins where it is set, so a part-resolved multi-step card previews
    /// against the event it is actually mid-way through rather than a newer window's.
    ///
    /// Null outside a reaction, and on any peer — every caller must tolerate that, which for a
    /// Targets() override means the null check it already needs to cast the event.
    ///
    /// A card says which window it belongs to by which of these it calls, rather than this asking
    /// IsBlockReaction: that property answers by building CardTriggers(), and a card whose triggers
    /// read the event back — StatusSyntheticFuel does — would recurse through here forever. Each card
    /// already knows its own window statically, so choosing at the call site is both cheaper and
    /// clearer than inferring it.
    /// </summary>
    protected ChangeEvent TriggerContext => ActivationTrigger ?? CardPlayPool.CurrentReactionTrigger;

    /// <inheritdoc cref="TriggerContext"/>
    protected T TriggerContextAs<T>() where T : ChangeEvent => TriggerContext as T;

    /// <summary>
    /// What the event being reacted to acts on — the whole answer for a card that chooses nothing
    /// because its trigger has already chosen for it: the "do not remove your X this turn" blocks,
    /// and the responses that hit or replace the very piece the trigger names.
    ///
    /// Delegates to the ChangeEvent's own Targets() rather than restating it, so these cards inherit
    /// the distinctions the events already draw — RemoveUnitChangeEvent, for one, reports the unit
    /// only while it is still on the board, which is what separates a block window (unit still there)
    /// from an after-reaction one (unit already gone, country still meaningful).
    ///
    /// Null-safe: outside a reaction window there is no trigger and this is TargetSet.None.
    /// </summary>
    protected TargetSet TriggerTargets() => TriggerContext.TargetsOrNone();

    /// <inheritdoc cref="TriggerContext"/>
    /// <remarks>The block-window half: the event a "do not remove your X this turn" card is offered
    /// against. RequestBlockReactions sets CurrentBlockTrigger and leaves CurrentReactionTrigger on
    /// the ENCLOSING window, so a block card reading the wrong one sees nothing at the top of a chain
    /// and the wrong region inside a nested one.</remarks>
    protected ChangeEvent BlockContext => ActivationTrigger ?? CardPlayPool.CurrentBlockTrigger;

    /// <inheritdoc cref="TriggerTargets"/>
    /// <remarks>The block-window half — see <see cref="BlockContext"/>.</remarks>
    protected TargetSet BlockTargets() => BlockContext.TargetsOrNone();

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
        if (IsBlocked)
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

    public bool IsBlocked {
        get { return CardState.IsBlocked; }
        set { CardState.IsBlocked = value; }
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

    /// <summary>
    /// What this card would affect if it were used right now, for the hover preview that lights its
    /// targets up while the player is still choosing a card. Declares nothing by default — an opt-in
    /// hook like <see cref="CardTriggers"/> and <see cref="PlayActionGuidance"/>.
    ///
    /// Override it with the SAME expression the card's steps select from — a private member reused by
    /// both, which is what keeps the preview honest. See <c>BuildArmy</c> for the canonical shape.
    ///
    /// Presentation only: nothing in the execution path reads this, and returning
    /// <see cref="TargetSet.None"/> is always safe.
    ///
    /// A card with several steps reports whatever it chooses to report — usually the union of
    /// everything it might touch, which answers "where does this card operate?". A card that wants to
    /// narrow that to the step about to run can read <see cref="ExecutableCardSteps"/> or
    /// <see cref="NextStepId"/> itself; there is deliberately no house rule, because steps a card
    /// appends to itself mid-execution (EventBroadFront, EventTheAutobahn) do not exist at hover time
    /// and no single rule covers them.
    ///
    /// HOST ONLY, in practice: an override may read CardPlayPool.CurrentReactionTrigger, which is null
    /// on a peer that holds no CardPlayRound. Clients receive the resolved result over the wire on
    /// <see cref="InputRequest.CardTargetPreviews"/> rather than recomputing it.
    /// </summary>
    public virtual TargetSet Targets() => TargetSet.None;

    /// <summary>
    /// Re-arm this card for a new turn. Called by ChangeRoundChangeEvent rather than driven by the
    /// EventBus NewTurnStarted signal, so it also reaches clients and a replayed save.
    /// </summary>
    public void OnNewTurnStarted(int turnNumber)
    {
        if (IsStatus)
            CardSteps.ForEach(step =>
            {
                step.StepFinished = false;
                // Cleared with StepFinished: a step re-armed for this turn must not still count last
                // turn's success as satisfying a CardStep.RequiringPreviousStep prerequisite.
                step.StepSucceeded = false;
            });

        // Safety net: DoCard releases the binding when the card's steps finish, but an activation
        // abandoned mid-way (exception, aborted epoch) would otherwise leave it dangling.
        ActivationTrigger = null;
        IsBlocked = false;
    }

    /// <summary>
    /// Re-arm this card as unused, for a card RETURNING to a deck or a hand from play — see
    /// RecycleCardChangeEvent, its only caller.
    ///
    /// CardStep.StepFinished is set once and never cleared again for anything but a Status card
    /// (<see cref="OnNewTurnStarted"/> re-arms those every turn, because their steps are meant to fire
    /// once per turn from the table). A recycled card is a different case that looked the same: the
    /// Build Army card Women Conscripts puts back on top of the draw deck kept every step marked
    /// finished, so once drawn again it had no executable steps, CanBeActivated returned false, and it
    /// sat in hand permanently unplayable. Same for Rationing shuffling a card back in, and for the
    /// discarded Build Army card Guards recycles to hand and plays on the spot — that one would have
    /// been discarded again without deploying anything.
    ///
    /// Deliberately does NOT touch CardState.ActivatedInTurns or PlayedInTurn: those gate real rules
    /// (MultipleActivationsPerTurn, the once-per-turn conditions), and clearing them would let a card
    /// recycled mid-turn be used a second time in the same turn. See the note on CardState.IsRevealed,
    /// which is re-hidden for exactly the same reason this re-arms, and by the same event.
    ///
    /// IsBlocked is likewise left alone — OnNewTurnStarted clears it, and lifting a block the moment a
    /// card moves piles would let a blocked card's remaining steps run in DoCard's loop.
    ///
    /// Ordering an in-round caller must respect: a card re-armed while it is still in the live
    /// CardPlayRound's CardPool has executable steps again, and CardPlayRound.ContinueWithNextSteps
    /// walks that pool. Guards and Flexible Resources both DoCard the recycled card immediately, which
    /// finishes the steps before any window can reach them; Women Conscripts and Rationing recycle
    /// from a step mutator, after the play step's round is already finished.
    /// </summary>
    public void OnReturnedToPlay()
    {
        CardSteps.ForEach(step =>
        {
            step.StepFinished = false;
            step.StepSucceeded = false;
        });
        IsPlayFinished = false;
        IsActivationFinished = false;
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
