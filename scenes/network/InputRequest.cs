using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(SelectCountryRequestHandler),       "SelectCountry")]
[JsonDerivedType(typeof(HandCardPlayRequestHandler),        "RequestHandCardPlay")]
[JsonDerivedType(typeof(ActivateCardRequestHandler),        "ActivateCard")]
[JsonDerivedType(typeof(HandCardsDiscardRequestHandler),    "RequestHandCardsDiscard")]
[JsonDerivedType(typeof(CardsRequestHandler),               "RequestCards")]
[JsonDerivedType(typeof(ForceDiscardHandCardsRequestHandler), "ForceDiscardHandCards")]
[JsonDerivedType(typeof(SelectUnitRequestHandler),           "SelectUnit")]
[JsonDerivedType(typeof(SelectBattleTargetRequestHandler),   "SelectBattleTarget")]
[JsonDerivedType(typeof(SelectFactionRequestHandler),        "SelectFaction")]
[JsonDerivedType(typeof(SelectOptionRequestHandler),         "SelectOption")]
[JsonDerivedType(typeof(ReorderCardsRequestHandler),         "ReorderCards")]
[JsonDerivedType(typeof(BlockReactionRequestHandler),        "BlockReaction")]
public abstract partial class InputRequest
{   
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public int TargetPeer => PlayerFactionRegistry.GetPeerIdForFaction(TargetFaction);
    // A dedicated/headless server controls no faction and has no local PlayerScene, so no input
    // request is ever "for" it — guard the null so the server can run this on its CallLocal path.
    public bool IsForCurrentPeer => PlayerScene.Current != null && TargetPeer == PlayerScene.Current.GetMultiplayerAuthority();
    public Faction TargetFaction { get; set; }

    public List<int> TargetCountryIds { get; set; }
    public List<int> TargetUnitIds { get; set; }
    public List<int> TargetCardIds { get; set; }
    public List<int> TargetStepIds { get; set; }
    public List<Faction> TargetFactions { get; set; }
    public List<string> TargetOptionLabels { get; set; }

    public List<int> ResponseCountryIds { get; set; } = new();
    public List<int> ResponseUnitIds { get; set; } = new();
    public List<int> ResponseCardIds { get; set; } = new();
    public List<Faction> ResponseFactions { get; set; } = new();
    public List<int> ResponseStepIds { get; set; } = new();

    public int TriggerCardId { get; set; } = -1;
    public string TriggerSummaryText { get; set; }

    /// <summary>
    /// Set by <see cref="BroadCast"/> when this request is raised from inside a scenario mutator's
    /// Run(), so the player being asked to pick a unit or a country can see which Bulletin is asking.
    /// Null for every other request. Serialized with the rest of the request — the base class carries
    /// the [JsonPolymorphic] attribute, so properties added here round-trip with no registration.
    /// </summary>
    public string TriggerBulletinLabel { get; set; }
    public string TriggerBulletinText { get; set; }

    /// <summary>
    /// How long the host will wait for this attempt before releasing the prompt and asking for a
    /// Retry / Skip decision. Stamped by <c>NetworkApi.SendInputRequest</c> per attempt, so a retry
    /// restarts the countdown; drives <see cref="InputTimerDisplay"/> on every peer. Zero means
    /// "no deadline to show" — the display stays hidden.
    /// </summary>
    public int TimeoutSeconds { get; set; }

    /// <summary>
    /// Set by a selection handler's <see cref="Handle"/> when the player skipped the
    /// input instead of making a choice. Serialized so it survives the DTO round-trip;
    /// <see cref="BroadCast"/> throws <see cref="StepSkippedException"/> when it is true.
    /// Mandatory-input handlers (discards) never set this, so those inputs stay required.
    /// </summary>
    public bool WasSkipped { get; set; } = false;

    /// <summary>
    /// True when this card prompt is a reaction window — an after-reaction or a block — rather than
    /// the faction's own reaction-depth-0 play. Set by the host; decides whether the client offers
    /// the scoped skip buttons alongside the plain Skip.
    /// </summary>
    public bool IsReactionWindow { get; set; } = false;

    /// <summary>
    /// Cards the prompt should DISPLAY, as opposed to <see cref="TargetCardIds"/>, which is what may
    /// be chosen. Everything here but not in TargetCardIds renders greyed out and unclickable.
    ///
    /// A reaction window carries the faction's whole event-triggered table
    /// (<see cref="CardPlayRound.ReactionWindowDisplayCardIds"/>) so the player can see why nothing
    /// of theirs applies, rather than facing a prompt that silently omits cards they know they hold.
    /// Null everywhere else, which means "display exactly the selectable set".
    /// </summary>
    public List<int> DisplayCardIds { get; set; }

    /// <summary>
    /// Set by a reaction prompt when the player chose one of the scoped skip buttons instead of
    /// plain Skip. Rides the same DTO round-trip as <see cref="WasSkipped"/>; the host feeds it to
    /// <see cref="GameFlow.RecordReactionSkip"/> and stops opening the information-hiding reaction
    /// windows for that faction for the scope's duration. NONE for every non-reaction request.
    /// </summary>
    public ReactionSkipScope ReactionSkipScope { get; set; } = ReactionSkipScope.NONE;

    public InputRequest(Faction targetFaction)
    {
        TargetFaction = targetFaction;
    }

    public static InputRequest FromJson(string jsonDto) => JsonSerializer.Deserialize<InputRequest>(jsonDto);
    public string ToJson() => JsonSerializer.Serialize(this);

    public async Task Execute()
    {
        if(IsForCurrentPeer)
        {
            // Showing the Bulletin here rather than in each Handle() covers every request subclass at
            // once — including the unit and country selections a mutator actually raises, which have
            // never had any trigger context of their own.
            bool showedBulletin = false;
            if (!string.IsNullOrEmpty(TriggerBulletinLabel))
            {
                TriggerContextDisplay.Current?.ShowBulletin(
                    CardFace.Bulletin(TriggerBulletinLabel, TriggerBulletinText), TriggerBulletinLabel);
                showedBulletin = true;
            }

            InputTimerDisplay.Current?.Start(TimeoutSeconds, "Your input");

            try
            {
                // Through the seam rather than Handle() directly, so a headless/scripted peer can
                // answer without the UI singletons Handle() reaches into. GodotInputProvider is a
                // pass-through to Handle(), so the GUI path is unchanged.
                await InputServices.Provider.Resolve(this);
            }
            finally
            {
                // Also on skip and on throw — a stale Bulletin next to an unrelated prompt is worse
                // than none.
                if (showedBulletin) TriggerContextDisplay.Current?.Hide();
                InputTimerDisplay.Current?.Hide();
            }
        }
        else
        {
            AwaitedFactions.Add(TargetFaction);
            RenderAwaitingText();

            // No finally to hide it here: this branch returns immediately rather than awaiting anything,
            // so the countdown is cleared where the waiting text already is — NetworkApi's
            // InputRequestAnswered (this faction's prompt closed) and AbortInputRequest (the host gave
            // up on it).
            InputTimerDisplay.Current?.Start(TimeoutSeconds, $"Waiting on {AwaitingLabel()}");
        }
    }

    // ── "Waiting on …" bookkeeping ───────────────────────────────────────────────
    //
    // A set rather than a single name. A reaction window takes a whole TEAM's turn at once
    // (CardPlayRound.TakeTeamTurn), so up to three requests are open together on different peers, and
    // each one arriving used to overwrite the label — a watcher was told it was waiting on whichever
    // request happened to land last. Maintained on every peer: the watcher branch above adds, and
    // NetworkApi.InputRequestAnswered removes as each prompt closes.

    private static readonly HashSet<Faction> AwaitedFactions = new();

    /// <summary>One of the prompts this peer was watching has closed — answered, withdrawn or given up on.</summary>
    public static void MarkInputClosed(Faction faction)
    {
        if (!AwaitedFactions.Remove(faction)) return;
        RenderAwaitingText();
        // Only once nothing is left, so the first answer of a concurrent team turn does not blank the
        // countdown of the players still deciding.
        if (AwaitedFactions.Count == 0) InputTimerDisplay.Current?.Hide();
    }

    /// <summary>Drop everything, for a session ending with prompts still nominally open.</summary>
    public static void ClearAwaitingInput()
    {
        if (AwaitedFactions.Count == 0) return;
        AwaitedFactions.Clear();
        RenderAwaitingText();
        InputTimerDisplay.Current?.Hide();
    }

    private static void RenderAwaitingText()
    {
        if (AwaitedFactions.Count == 0)
        {
            PresentationServices.Notification.HideActionText();
            return;
        }
        PresentationServices.Notification.ShowActionText($"Waiting on {AwaitingLabel()} input...");
    }

    private static string AwaitingLabel() =>
        string.Join(", ", AwaitedFactions.Select(f => f.WithPlayer()));

    public async Task<InputRequest> BroadCast()
    {
        DebugUtilities.PrintPeer($"Broadcasting input request {GetType().Name} to {TargetFaction}");

        // Stamp the running mutator's Bulletin on the way out. BroadCast is the single chokepoint every
        // request passes through, and TriggerCardId taking precedence keeps card reactions unchanged.
        if (TriggerCardId == -1 && StepMutatorRunner.RunningBulletin is { } bulletin)
        {
            TriggerBulletinLabel = bulletin.Label;
            TriggerBulletinText = bulletin.Text;
        }

        InputRequest responseDto = await NetworkApi.Instance.SendInputRequest(this);
        if (responseDto.WasSkipped)
        {
            DebugUtilities.PrintPeer($"Input request {GetType().Name} was skipped by {TargetFaction}");
            throw new StepSkippedException();
        }
        return responseDto;
    }


    public abstract Task Handle();

    /// <summary>
    /// Fill the Target* fields with this request's legal move set, on the host, before the request
    /// goes on the wire. Four subclasses computed their option list inside Handle() from live state
    /// instead of carrying it, which left the request undescribable to anything but the local UI.
    ///
    /// Called from <c>NetworkApi.SendInputRequest</c> — NOT from <c>BroadCast</c>, because
    /// <c>CardPlayRound.RequestPlay</c> and <c>RequestBlock</c> call SendInputRequest directly and
    /// those are exactly the two paths that build the requests needing this.
    ///
    /// Additive only: the GUI Handle() implementations still recompute locally and ignore these
    /// fields, so this cannot change GUI behaviour.
    /// </summary>
    public virtual void PopulateTargets() { }

    /// <summary>
    /// Await the CardSelected signal, releasing the prompt as a pass if the host abandons the request.
    /// The three card-selection requests all used a bare <c>GetSignalAwaiter</c>, which nothing but a
    /// real click could ever complete — so an aborted request left the hand live and clickable forever.
    ///
    /// Cancelling emits CardSelected(-1), the same value the Skip button produces, so every caller's
    /// existing pass handling applies unchanged.
    /// </summary>
    protected static async Task<Variant[]> AwaitCardSelection()
    {
        using (PendingLocalInput.Register(() => InputManager.Current?.CancelCardSelection()))
        {
            return await EventBus.GetSignalAwaiter("CardSelected");
        }
    }

    public class SelectCountryRequestHandler : InputRequest
    {
        public SelectCountryRequestHandler(Faction targetFaction, List<int> targetCountryIds) : base(targetFaction)
        {
            TargetCountryIds = targetCountryIds;
        }

        public override async Task Handle()
        {
            int countryId = await new SelectCountryHandler(TargetCountryIds).Handle();
            if (countryId == -1)
            {
                WasSkipped = true;
                return;
            }
            ResponseCountryIds.Add(countryId);
        }
    }

    public class HandCardPlayRequestHandler : InputRequest
    {
        public HandCardPlayRequestHandler(Faction targetFaction) : base(targetFaction) {}

        // The initial play at reaction depth 0. Selectable = ActivatableCardIds, which for a hand card
        // resolves to CardLogic.CanBeActivated — the play conditions AND HasExecutableCardSteps. The
        // hand used to be concatenated in wholesale, which offered a card whose every step condition
        // was false; playing it just reported "Unable to" on each step in turn and did nothing.
        //
        // The hand is still DISPLAYED, greyed out, so an unplayable card is visibly unplayable rather
        // than missing. The client cannot narrow this itself: Tag.IsExecutable is server-internal and
        // never replicated (see GameStateCalculator.ReplicatedTags).
        //
        // ??= so a caller can hand over its own set — EventLendLease grants an out-of-turn play, where
        // ActivatableCardIds is empty for the receiving faction (IsFactionTurn fails) and the whole
        // hand is the correct offer.
        public override void PopulateTargets()
        {
            DeckState deck = DeckState.ForFaction(TargetFaction);
            TargetCardIds ??= deck.ActivatableCardIds;
            DisplayCardIds ??= deck.ActivatableCardIds.Concat(deck.HandCardIds).Distinct().ToList();
        }

        public override async Task Handle()
        {
            // The host's list, not a local re-derivation — same rule as BlockReactionRequestHandler.
            //
            // separateNonHandCards: this is the one prompt whose offer spans two zones, so the table
            // cards that activate instead of a hand play get their own smaller fan beside the hand
            // rather than being interleaved into it by card id.
            PlayerScene.Current.InputManager.SetCardSelectionActive(
                TargetFaction, TargetCardIds ?? new List<int>(), false, DisplayCardIds,
                separateNonHandCards: true);
            Variant[] results = await AwaitCardSelection();
            if (results != null && results.Length > 0)
            {
                ResponseCardIds.Add((int)results[0]);
            }
        }
    }

    public class ActivateCardRequestHandler : InputRequest
    {
        public ActivateCardRequestHandler(Faction targetFaction) : base(targetFaction) {}

        // Table cards only, no hand — and never overwrites a list the caller already set:
        // CardPlayRound.RequestPlay stamps the exact
        // after-reaction options for a reaction window, and ActivatableCardIds is a wider set.
        public override void PopulateTargets()
            => TargetCardIds ??= DeckState.ForFaction(TargetFaction).ActivatableCardIds;

        public override async Task Handle()
        {
            // The host's list, not a local re-derivation — same rule as BlockReactionRequestHandler.
            //
            // No separateNonHandCards here, unlike HandCardPlayRequestHandler: everything this prompt
            // offers is already a table card, so splitting it out would leave the hand position empty.
            // A reaction window draws ReactionWindowDisplayCardIds (the Status and Response piles), and
            // outside the play window a hand card cannot be IsActivatable at all —
            // CardLogic._defaultPlayConditions requires PLAY_CARD, this faction's turn, and nothing
            // played yet, which is exactly when RequestPlay sends the hand-play request instead.
            PlayerScene.Current.InputManager.SetCardSelectionActive(
                TargetFaction, TargetCardIds ?? new List<int>(), IsReactionWindow, DisplayCardIds);
            if (TriggerCardId > -1)
                TriggerContextDisplay.Current?.ShowCard(TriggerCardId, TriggerSummaryText);
            DebugUtilities.PrintPeer($"ActivateCard: Waiting for player input.");
            Variant[] results = await AwaitCardSelection();
            ReactionSkipScope = PlayerScene.Current.InputManager.TakeReactionSkipScope();
            if (results != null && results.Length > 0)
            {
                ResponseCardIds.Add((int)results[0]);
            }
        }
    }

    public class HandCardsDiscardRequestHandler : InputRequest
    {
        public HandCardsDiscardRequestHandler(Faction targetFaction) : base(targetFaction) {}

        // InputHandlerDiscardHand reads the hand itself; carry it so the request is self-describing.
        public override void PopulateTargets()
            => TargetCardIds = new List<int>(DeckState.ForFaction(TargetFaction).HandCardIds);

        public override async Task Handle()
        {
            InputHandlerDiscardHand inputHandler = new InputHandlerDiscardHand(TargetFaction);
            List<int> selectedCardIds = await inputHandler.GetSelectedCards();            
            ResponseCardIds = selectedCardIds;
        }
    }

    public class CardsRequestHandler : InputRequest
    {
        public CardsRequestHandler(Faction targetFaction, List<int> targetCardIds) : base(targetFaction)
        {
            TargetCardIds = targetCardIds;
        }

        public override async Task Handle()
        {            
            InputHandlerDiscard inputHandler = new InputHandlerDiscard(TargetFaction, TargetCardIds, 0, false);
            List<int> selectedCardIds = await inputHandler.GetSelectedCards();            
            ResponseCardIds = selectedCardIds;
        }
    }

    /// <summary>
    /// Sent to the peer controlling <see cref="InputRequest.TargetFaction"/>.
    /// Only that client's Handle() runs; all others wait.
    /// </summary>
    public class ForceDiscardHandCardsRequestHandler : InputRequest
    {
        public int NumberOfCards { get; set; }

        public ForceDiscardHandCardsRequestHandler(Faction targetFaction, int numberOfCards) : base(targetFaction)
        {
            NumberOfCards = numberOfCards;
        }

        // Handle() reads the hand directly; carry it so the request is self-describing.
        public override void PopulateTargets()
            => TargetCardIds = new List<int>(DeckState.ForFaction(TargetFaction).HandCardIds);

        public override async Task Handle()
        {
            List<int> handCardIds = DeckState.ForFaction(TargetFaction).HandCardIds;
            InputHandlerDiscard inputHandler = new InputHandlerDiscard(TargetFaction, handCardIds, NumberOfCards, true);
            List<int> selectedCardIds = await inputHandler.GetSelectedCards();
            ResponseCardIds = selectedCardIds;
        }
    }

    public class SelectUnitRequestHandler : InputRequest
    {
        public SelectUnitRequestHandler(Faction targetFaction, List<int> targetUnitIds) : base(targetFaction)
        {
            TargetUnitIds = targetUnitIds;
        }

        public override async Task Handle()
        {
            int unitId = await new SelectUnitHandler(TargetUnitIds).Handle();
            if (unitId == -1)
            {
                WasSkipped = true;
                return;
            }
            ResponseUnitIds.Add(unitId);
        }
    }

    public class SelectBattleTargetRequestHandler : InputRequest
    {
        [JsonConstructor]
        public SelectBattleTargetRequestHandler(Faction targetFaction, List<int> targetCountryIds, List<int> targetUnitIds) : base(targetFaction)
        {
            TargetCountryIds = targetCountryIds;
            TargetUnitIds = targetUnitIds;
        }

        public SelectBattleTargetRequestHandler(Faction targetFaction, List<BattleTarget> targets) : base(targetFaction)
        {
            TargetCountryIds = targets.Where(bt => bt.Type == TargetType.COUNTRY).Select(bt => bt.Id).ToList();
            TargetUnitIds = targets.Where(bt => bt.Type == TargetType.UNIT).Select(bt => bt.Id).ToList();
        }

        public override async Task Handle()
        {
            SelectBattleTargetHandler handler = new SelectBattleTargetHandler(TargetCountryIds, TargetUnitIds);
            BattleTarget result = await handler.Handle();
            if (result == null)
            {
                WasSkipped = true;
                return;
            }
            if (result.Type == TargetType.COUNTRY)
                ResponseCountryIds.Add(result.Id);
            else
                ResponseUnitIds.Add(result.Id);
        }
    }

    public class SelectFactionRequestHandler : InputRequest
    {
        [JsonConstructor]
        public SelectFactionRequestHandler(Faction targetFaction, List<Faction> targetFactions) : base(targetFaction)
        {
            TargetFactions = targetFactions;
        }

        public override async Task Handle()
        {
            var items = PresentationItem.ForFactions(TargetFactions);
            ModalResult result = await ModalStack.Current.Show(
                ModalConfig.SelectOne("Select a faction", items));
            // Count as well as WasCancelled: SelectOne's default gates Apply behind one selection, so an
            // empty result is unreachable today — but indexing [0] on one is an exception thrown out of
            // card execution, and a SelectOne(required: false) would make it reachable.
            if (result.WasCancelled || result.SelectedItems.Count == 0)
            {
                WasSkipped = true;
                return;
            }
            ResponseCardIds.Add(result.SelectedItems[0]);
        }
    }

    public class SelectOptionRequestHandler : InputRequest
    {
        public List<int> TargetOptionIds { get; set; } = new();
        public string ModalTitle { get; set; }

        [JsonConstructor]
        public SelectOptionRequestHandler(Faction targetFaction, List<string> targetOptionLabels, List<int> targetOptionIds, string modalTitle) : base(targetFaction)
        {
            TargetOptionLabels = targetOptionLabels;
            TargetOptionIds = targetOptionIds;
            ModalTitle = modalTitle;
        }

        // Convenience: auto 0-based identifiers
        public SelectOptionRequestHandler(Faction targetFaction, List<string> targetOptionLabels, string modalTitle)
            : this(targetFaction, targetOptionLabels,
                   Enumerable.Range(0, targetOptionLabels.Count).ToList(), modalTitle) { }

        public override async Task Handle()
        {
            var items = TargetOptionLabels
                .Select((label, i) => (PresentationItem)new PresentationItemTextButton(TargetOptionIds[i], label, true))
                .ToList();
            ModalResult result = await ModalStack.Current.Show(ModalConfig.SelectOne(ModalTitle, items));
            // See SelectFactionRequestHandler: guarded against an empty result for the same reason.
            if (result.WasCancelled || result.SelectedItems.Count == 0)
            {
                WasSkipped = true;
                return;
            }
            ResponseCardIds.Add(result.SelectedItems[0]);
        }
    }

    public class ReorderCardsRequestHandler : InputRequest
    {
        public ReorderCardsRequestHandler(Faction targetFaction, List<int> targetCardIds) : base(targetFaction)
        {
            TargetCardIds = targetCardIds;
        }

        public override async Task Handle()
        {
            var items = PresentationItemCard.FromCardIds(TargetCardIds, true);
            ModalResult result = await ModalStack.Current.Show(
                ModalConfig.Reorder("Reorder the top cards of your draw deck", items));
            ResponseCardIds = result.WasCancelled ? new List<int>(TargetCardIds) : result.SelectedItems;
        }
    }

    /// <summary>
    /// Asks the target faction whether to play a block reaction (or pass). Only the controlling
    /// peer runs Handle(); the authoritative host awaits the response over the network, so a
    /// faction-less dedicated server never blocks on a local UI click. An empty ResponseCardIds
    /// (including an explicit skip/pass, card id -1) means "no block".
    /// </summary>
    public class BlockReactionRequestHandler : InputRequest
    {
        // A block prompt is always a reaction window, so the scoped skip buttons always apply.
        public BlockReactionRequestHandler(Faction targetFaction) : base(targetFaction)
        {
            IsReactionWindow = true;
        }

        public override async Task Handle()
        {
            await Task.Delay(GameSettings.DurationMedium);
            // Only the block-eligible cards the host sent — not every activatable card — may be chosen here.
            PlayerScene.Current.InputManager.SetCardSelectionActive(
                TargetFaction, TargetCardIds ?? new List<int>(), IsReactionWindow, DisplayCardIds);
            if (TriggerCardId > -1)
                TriggerContextDisplay.Current?.ShowCard(TriggerCardId, TriggerSummaryText);
            Variant[] results = await AwaitCardSelection();
            ReactionSkipScope = PlayerScene.Current.InputManager.TakeReactionSkipScope();
            if (results != null && results.Length > 0 && (int)results[0] > -1)
            {
                ResponseCardIds.Add((int)results[0]);
            }
        }
    }


    
}
