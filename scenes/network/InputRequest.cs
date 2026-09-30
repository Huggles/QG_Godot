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
[JsonDerivedType(typeof(SelectCardRequestHandler),          "SelectCard")]
[JsonDerivedType(typeof(SelectCardsRequestHandler),         "SelectCards")]
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

    /// <summary>
    /// The peer that will answer this prompt.
    ///
    /// Not the same as the registry id of the seat that owns the faction: an AI seat carries a
    /// synthetic id that matches no live peer, and its prompts are answered inside the host's process.
    /// GetAnsweringPeerForFaction performs that one translation so everything downstream of this
    /// property — IsForCurrentPeer here, the RpcId targets in NetworkApi.AbortRemoteInput,
    /// PendingInput.Peer, SafeTargetPeer — needs no knowledge of AI seats.
    /// </summary>
    public int TargetPeer => PlayerFactionRegistry.GetAnsweringPeerForFaction(TargetFaction);

    // A dedicated/headless server controls no faction and has no local PlayerScene, so no input
    // request is ever "for" it — guard the null so the server can run this on its CallLocal path.
    public bool IsForCurrentPeer => PlayerScene.Current != null && TargetPeer == PlayerScene.Current.GetMultiplayerAuthority();

    /// <summary>
    /// This peer answers the prompt, but a bot will — not the person at the keyboard.
    ///
    /// The one predicate the presentation in <see cref="Execute"/> branches on. Everything it gates
    /// there says "you are being asked" in some form — the input chime, the "Your input" countdown,
    /// the focus tint, dismissing the turn badge — and none of that is true when a bot is deciding.
    /// </summary>
    public bool IsForLocalBot => IsForCurrentPeer && PlayerFactionRegistry.IsFactionAi(TargetFaction);

    /// <summary>
    /// A reaction window that offers nothing — the always-ask rule at work. A faction holding a
    /// face-down Response card is prompted on every qualifying event so that being asked stops proving
    /// anything, which means dozens of these a turn with only one legal answer.
    ///
    /// Named because three places need exactly this test and must agree: the armed auto-pass below,
    /// the prompt-pacing delay in BlockReactionRequestHandler.Handle, and AiSeatInputProvider's think
    /// time — a bot pausing to consider each of dozens of unanswerable prompts would be unbearable.
    /// </summary>
    [JsonIgnore]
    public bool IsEmptyReactionWindow => IsReactionWindow && (TargetCardIds?.Count ?? 0) == 0;

    public Faction TargetFaction { get; set; }

    /// <summary>
    /// Every faction this one prompt answers for, when it answers for more than one.
    ///
    /// A reaction window takes a whole team's turn at once, and the factions of that team controlled by
    /// the SAME seat are now merged into a single request — see CardPlayRound.TakeTeamTurn. A player
    /// holding Germany, Japan and Italy answers once with every reaction the three of them can make
    /// instead of clicking through three prompts, two of which are usually empty always-ask windows.
    ///
    /// <see cref="TargetFaction"/> stays the representative of the group and keeps doing all the
    /// routing: every faction in a group shares a seat, so <see cref="TargetPeer"/>,
    /// <see cref="IsForCurrentPeer"/> and <see cref="IsForLocalBot"/> answer the same for any of them.
    /// This list is what the parts that speak to or about the PLAYER need — the banner, the scoped skip,
    /// the "Waiting on …" label and the host's per-faction skip recording.
    ///
    /// Null or empty on every other request in the game, which is what <see cref="Answering"/> covers.
    /// Deliberately not <see cref="TargetFactions"/>: that one is an OPTION set (the factions a
    /// SelectFaction prompt offers to choose between), and InputRequestSpec reads it as such.
    /// </summary>
    public List<Faction> AnsweringFactions { get; set; }

    /// <summary>
    /// The factions this prompt answers for, always non-empty: <see cref="AnsweringFactions"/> when the
    /// host merged a group, otherwise just <see cref="TargetFaction"/>. Every reader should use this
    /// rather than the raw list, so a single-faction prompt needs no special case anywhere.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<Faction> Answering =>
        AnsweringFactions is { Count: > 0 } ? AnsweringFactions : new List<Faction> { TargetFaction };

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
    /// Which window this prompt is — block, after-reaction, or neither. Decides the header
    /// <see cref="TriggerContextDisplay"/> writes over <see cref="TriggerSummaryText"/> and the banner
    /// <c>InputManager.SetCardSelectionActive</c> shows, so the player can tell "this is about to
    /// happen, stop it?" from "this happened, answer it?" — see <see cref="TriggerKind"/>.
    ///
    /// Stamped by the host where the window is opened, not inferred from the request subclass here: a
    /// reaction window and a faction's own reaction-depth-0 play both arrive as
    /// <see cref="ActivateCardRequestHandler"/>, and only the host knows which it built.
    /// </summary>
    public TriggerKind TriggerReactionKind { get; set; } = TriggerKind.NONE;

    /// <summary>
    /// What caused the triggering event, in words: "Build Army", "Blitzkrieg (Status card)". Straight
    /// off <c>GameMessageDisplay.CauseText</c>, which owns the wording and the redaction of a
    /// face-down Response card. Null for a trigger with no source card, which leaves the line off.
    ///
    /// Carried rather than derived from <see cref="TriggerCardId"/> on the client, for two reasons:
    /// that id falls back to the last card in the play pool when the event has no source card of its
    /// own, so it is the card to SHOW and not necessarily the cause; and a block window's trigger
    /// event has not been broadcast yet, so the client cannot inspect it at all.
    /// </summary>
    public string TriggerCauseText { get; set; }

    /// <summary>
    /// Where on the board the event that opened this prompt landed — the point the focus viewport
    /// centres on, and what it marks while the prompt is open. The two kinds are carried apart for the
    /// same reason <see cref="CardTargetPreview"/> keeps them apart: a country glows, a unit puts up
    /// its own marker, and collapsing a unit to its country lights the whole of Russia when the event
    /// only reached the one army standing there.
    ///
    /// Stamped on the host beside <see cref="TriggerCardId"/> from the trigger's
    /// <see cref="ChangeEvent.Targets"/> — a client holds no CardPlayRound and has no trigger to ask.
    /// Empty for a trigger that names no place (a card play, a Bulletin), which is what tells
    /// <see cref="TriggerContextDisplay"/> to leave the viewport hidden.
    /// </summary>
    public List<int> TriggerTargetCountryIds { get; set; }

    /// <inheritdoc cref="TriggerTargetCountryIds"/>
    public List<int> TriggerTargetUnitIds { get; set; }

    /// <summary>
    /// Set by <see cref="BroadCast"/> when this request is raised from inside a step mutator's Run(),
    /// so the player being asked to pick a unit or a country can see what is asking. Null for every
    /// other request. Serialized with the rest of the request — the base class carries the
    /// [JsonPolymorphic] attribute, so properties added here round-trip with no registration.
    /// </summary>
    public string TriggerBulletinLabel { get; set; }
    public string TriggerBulletinText { get; set; }

    /// <summary>
    /// The card behind the mutator that raised this request, or -1 when the scenario declared it (or
    /// no mutator is running). Shown instead of the Bulletin face, matching the announcement modal.
    ///
    /// Deliberately not <see cref="TriggerCardId"/>: that one is only rendered by the two card-choice
    /// handlers, whereas the block below sits in <see cref="Execute"/> and so covers every request
    /// subclass — including the unit and country selections a mutator actually raises.
    /// </summary>
    public int TriggerBulletinCardId { get; set; } = -1;

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
    /// What passing this prompt will cost the faction, phrased for the Skip button — "discard 1 card",
    /// "lose 1 VP". Null when passing is free, which is every prompt but the faction's own play.
    ///
    /// Carried on the wire rather than worked out by the client: the cost depends on the host's
    /// authoritative read of the hand, and keeping it here leaves room for a card to modify the cost
    /// later without the client having to know the rule.
    /// </summary>
    public string PassCostText { get; set; }

    /// <summary>
    /// The card whose step raised this prompt, or -1 when no step did (a turn-step handler, a mutator,
    /// the opening discard). Stamped host-side in <see cref="NetworkApi.SendInputRequest"/> from
    /// <see cref="PromptOrigin.Current"/>.
    ///
    /// Deliberately not <see cref="TriggerCardId"/>, which is the card being REACTED TO and is only
    /// populated inside a reaction window. This is the card doing the asking.
    /// </summary>
    public int OriginCardId { get; set; } = -1;

    /// <summary>
    /// Which of the origin card's steps is asking. A bot reads that step's PossibleOutcomes to know
    /// what each option would do.
    /// </summary>
    public int OriginStepId { get; set; } = -1;

    /// <summary>
    /// What KIND of step raised this prompt, i.e. why the card is asking: a <see cref="StepKind.Requirement"/> prompt is a cost the card is charging, a
    /// <see cref="StepKind.Result"/> prompt is the payoff it charges for.
    ///
    /// Before card steps were typed there was no way to tell the two apart without running the step
    /// — the complaint CardLogic.IsFreePlayStepActivation's summary spells out — so a bot could not
    /// price "discard 2 cards to deploy 1 army" and a UI could not warn about one.
    ///
    /// Defaults to <see cref="StepKind.Result"/> rather than to a "none" member: every prompt that
    /// is not a declared cost is, as far as a consumer is concerned, part of the effect, and a
    /// request raised outside any card step at all is not a cost either.
    /// </summary>
    public StepKind OriginStepKind { get; set; } = StepKind.Result;

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
    /// Per-card hover preview: what each card this prompt draws could affect, so hovering a card in
    /// the hand lights its targets up on the board before the player commits to it.
    ///
    /// Countries and units are carried apart rather than collapsed to countries, because they are
    /// drawn differently: a targeted country glows, a targeted unit puts up its own target marker.
    /// Collapsing a unit to the country it stands in — which is what this used to ship — lit the whole
    /// of Egypt when the card could only reach the one army standing there.
    ///
    /// Computed on the host in <see cref="PopulateTargets"/>, where the window's reaction trigger and
    /// freshly calculated tags are both live, from each card's <see cref="CardLogic.Targets"/>. A
    /// client cannot derive it: a peer holds no CardPlayRound, so CardPlayPool.CurrentReactionTrigger
    /// is null there and every reaction-scoped target set collapses to empty.
    ///
    /// A list rather than a Dictionary&lt;int, List&lt;int&gt;&gt; so System.Text.Json needs no key
    /// converter. Cards with no targets to show are omitted entirely rather than carried as empty
    /// entries.
    /// </summary>
    public List<CardTargetPreview> CardTargetPreviews { get; set; }

    /// <summary>One entry of <see cref="CardTargetPreviews"/>.</summary>
    public sealed class CardTargetPreview
    {
        public int CardId { get; set; }

        /// <summary>Countries the card names outright. NOT the countries its unit targets stand in.</summary>
        public List<int> CountryIds { get; set; }

        /// <summary>Units the card names. Each lights its own marker, leaving its country dark.</summary>
        public List<int> UnitIds { get; set; }

        /// <summary>True when there is nothing to draw, so the host can leave the entry off the wire.</summary>
        // Ignored rather than merely unused on the far side: System.Text.Json serializes get-only
        // properties, so without this every entry would carry a field the reader recomputes anyway.
        [JsonIgnore]
        public bool IsEmpty => (CountryIds == null || CountryIds.Count == 0)
                               && (UnitIds == null || UnitIds.Count == 0);
    }

    /// <summary>
    /// Set by a reaction prompt when the player chose one of the scoped skip buttons instead of
    /// plain Skip. Rides the same DTO round-trip as <see cref="WasSkipped"/>; the host feeds it to
    /// <see cref="GameFlow.RecordReactionSkip"/> and stops opening the information-hiding reaction
    /// windows for that faction for the scope's duration. NONE for every non-reaction request.
    ///
    /// One scope for the whole prompt, which on a merged window means every faction in
    /// <see cref="Answering"/>: the player made one decision about one prompt, and the per-faction
    /// control they already have is the toggle in each faction's info row.
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
            // Showing the mutator's context here rather than in each Handle() covers every request
            // subclass at once — including the unit and country selections a mutator actually raises,
            // which have never had any trigger context of their own.
            bool showedBulletin = false;
            if (TriggerBulletinCardId > -1)
            {
                // A card put this mutator in play, so show that card — the same one the announcement
                // modal just showed — rather than a Bulletin standing in for it.
                // BULLETIN rather than TriggerReactionKind: this block is not a reaction window at all,
                // and it knows that locally — no host field needed to tell it so.
                TriggerContextDisplay.Current?.ShowCard(
                    TriggerBulletinCardId, TriggerBulletinLabel, TriggerKind.BULLETIN);
                showedBulletin = true;
            }
            else if (!string.IsNullOrEmpty(TriggerBulletinLabel))
            {
                TriggerContextDisplay.Current?.ShowBulletin(
                    CardFace.Bulletin(TriggerBulletinLabel, TriggerBulletinText), TriggerBulletinLabel);
                showedBulletin = true;
            }

            // Everything in this block says "you are being asked" in one form or another, so none of
            // it is true when a bot on this peer is the one deciding. One flag rather than four
            // conditionals, and the same flag guards the matching teardown in the finally — two of
            // those are concurrency fixes rather than cosmetics, see there.
            bool asked = !IsForLocalBot;

            // Inside the IsForCurrentPeer branch on purpose: only the player actually being asked
            // hears it. Every request subclass funnels through here, so this covers card, country,
            // unit and battle-target prompts without a cue per handler.
            if (asked) AudioManager.PlaySfxSetting(AudioManager.InputRequestSetting);

            // Suppressed rather than relabelled for a bot: TimeoutSeconds is the host's 15-minute
            // backstop, and a fifteen-minute countdown over a decision that takes a second misinforms.
            // AiSeatInputProvider renders "Waiting on <faction> input…" instead, which carries no clock.
            if (asked) InputTimerDisplay.Current?.Start(TimeoutSeconds, "Your input");

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
                // than none. Not gated on `asked`: the bulletin IS shown for a bot's prompt, because
                // "Blitzkrieg — Germany is deciding" is exactly the narration a watching human wants.
                if (showedBulletin) TriggerContextDisplay.Current?.Hide();

                // Gated for correctness, not tidiness. A team reaction window opens prompts for a
                // whole team at once, so on a host holding one human faction and one bot faction on
                // the same team this runs while the HUMAN's prompt is still open — an ungated Hide()
                // would clear their live countdown.
                if (asked) InputTimerDisplay.Current?.Hide();

                // The player has answered and the game is moving on, so a turn announcement still
                // fading out over the middle of the screen is behind the play — drop it rather than
                // letting it ride out the rest of its fade. In the finally with the rest: a skipped or
                // timed-out prompt is just as much a reason for the badge to be gone.
                //
                // Gated, and this single line is what makes a bot's turn legible: the badge runs
                // fadeIn+hold+fadeOut over three DurationLongs, and a bot answers its first prompt in
                // the same frame the badge starts — so without this the human never sees whose turn it
                // is for any AI faction. ShowTurnBadgeAnimation sets BlockQueue = false, so letting it
                // ride costs the turn loop nothing.
                if (asked) TurnBadge.DismissNow();
            }
        }
        else
        {
            // Every faction the prompt answers for, not just its representative: a merged window left
            // its other factions in the set forever, and MarkInputClosed only hides the countdown once
            // the set empties.
            MarkInputOpen(Answering);

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

    /// <summary>
    /// Start showing this peer that a faction is being waited on.
    ///
    /// The watcher branch of <see cref="Execute"/> does this inline for a REMOTE faction's prompt. It
    /// is extracted so a bot seat can borrow the same treatment while it thinks: from the human's
    /// point of view "Waiting on Germany input…" is exactly true, even though the prompt is being
    /// answered inside their own process. Paired with <see cref="MarkInputClosed"/>, which is
    /// idempotent, so the later AnnounceInputClosed broadcast is harmless.
    /// </summary>
    public static void MarkInputOpen(Faction faction) => MarkInputOpen(new[] { faction });

    /// <inheritdoc cref="MarkInputOpen(Faction)"/>
    /// <remarks>One prompt may answer for several factions — see <see cref="AnsweringFactions"/>.</remarks>
    public static void MarkInputOpen(IReadOnlyList<Faction> factions)
    {
        bool added = false;
        foreach (Faction faction in factions) added |= AwaitedFactions.Add(faction);
        if (!added) return;
        RenderAwaitingText();
    }

    /// <summary>One of the prompts this peer was watching has closed — answered, withdrawn or given up on.</summary>
    public static void MarkInputClosed(Faction faction) => MarkInputClosed(new[] { faction });

    /// <inheritdoc cref="MarkInputClosed(Faction)"/>
    public static void MarkInputClosed(IReadOnlyList<Faction> factions)
    {
        bool removed = false;
        foreach (Faction faction in factions) removed |= AwaitedFactions.Remove(faction);
        if (!removed) return;
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

        // Stamp the running mutator's context on the way out. BroadCast is the single chokepoint every
        // request passes through, and TriggerCardId taking precedence keeps card reactions unchanged.
        if (TriggerCardId == -1 && StepMutatorRunner.RunningBulletin is { } bulletin)
        {
            TriggerBulletinLabel = bulletin.Label;
            TriggerBulletinText = bulletin.Text;
            TriggerBulletinCardId = bulletin.SourceCardId;
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
    /// Fill <see cref="CardTargetPreviews"/> for every card this prompt draws. Called at the end of
    /// the card prompts' <see cref="PopulateTargets"/>, which is the one moment the host has both the
    /// offer set and the window's live context.
    ///
    /// Runs over the DISPLAY set, not the selectable one: a greyed-out card lighting up nothing is
    /// itself the answer to "why can't I play this?", and it costs nothing to compute.
    /// </summary>
    protected void PopulateCardTargetPreviews()
    {
        List<int> cardIds = DisplayCardIds ?? TargetCardIds;
        if (cardIds == null) return;

        CardTargetPreviews = cardIds
            .Distinct()
            .Select(cardId =>
            {
                // TargetsOrNone, not Targets: a target expression evaluated a moment before its step
                // can legitimately throw, and that must cost the preview and not the prompt.
                TargetSet targets = CardState.ForId(cardId)?.CardLogic?.TargetsOrNone() ?? TargetSet.None;
                return new CardTargetPreview
                {
                    CardId = cardId,
                    // The two kinds stay apart all the way to the board — see CardTargetPreview. Card
                    // and Faction targets are dropped here: neither names a place to light up.
                    CountryIds = targets.CountryIds.ToList(),
                    UnitIds = targets.UnitIds.ToList(),
                };
            })
            .Where(preview => !preview.IsEmpty)
            .ToList();
    }

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

    /// <summary>
    /// Answer this reaction window as a pass without ever drawing it, when the player has pre-armed a
    /// <see cref="ReactionSkipPreference"/> and the window offers nothing. Returns true if it did, in
    /// which case the caller must return immediately.
    ///
    /// <para>
    /// Gated on an EMPTY offer on purpose. A window with real options must always reach the player,
    /// including under <see cref="ReactionSkipScope.UNTIL_ACTIVATABLE"/> — being told the moment
    /// something becomes usable is the whole point of that setting, and for TURN_STEP/ROUND the host
    /// makes the same carve-out for a publicly visible table card in
    /// <c>CardPlayRound.ShouldOpenReactionWindow</c>.
    /// </para>
    ///
    /// <para>
    /// The armed scope rides back on <see cref="ReactionSkipScope"/>. TURN_STEP and ROUND are recorded
    /// by <see cref="GameFlow.RecordReactionSkip"/> and the host stops opening these windows from then
    /// on; UNTIL_ACTIVATABLE is ignored there, so every window keeps opening and the "Waiting on X
    /// input…" line the other players see stays exactly where it was. Empty ResponseCardIds is a pass.
    /// </para>
    ///
    /// <para>
    /// Called BEFORE <c>SetCardSelectionActive</c>, not from inside it: <see cref="AwaitCardSelection"/>
    /// registers its awaiter only after that call returns, so a CardSelected emitted from within it
    /// would be missed — and returning early is also what keeps this flicker-free, with no prompt
    /// built and no trigger context shown.
    /// </para>
    /// </summary>
    protected bool TryAutoPassArmedReactionWindow()
    {
        if (!IsEmptyReactionWindow) return false;

        // All or nothing across a merged prompt. One press of a scoped skip button arms every faction
        // the prompt answers for, but the standing preference is set per faction in the faction info
        // rows — so a group can hold one armed faction beside one that never asked to be silenced, and
        // auto-passing there would answer a window on behalf of a player who wanted the click.
        ReactionSkipScope armed = ReactionSkipScope.NONE;
        foreach (Faction faction in Answering)
        {
            ReactionSkipScope factionScope = ReactionSkipPreference.ActiveScope(faction);
            if (factionScope == ReactionSkipScope.NONE) return false;
            armed = Narrower(armed, factionScope);
        }

        DebugUtilities.PrintPeer($"{string.Join(", ", Answering)} auto-passed an empty reaction window ({armed})");
        ReactionSkipScope = armed;
        return true;
    }

    /// <summary>
    /// The less silencing of two armed scopes, for a merged prompt whose factions armed different ones.
    /// The response carries one scope for the whole group (see <see cref="ReactionSkipScope"/>), so it
    /// has to be the one that takes the fewest windows away: a faction that asked for TURN_STEP must not
    /// be handed a teammate's ROUND. UNTIL_ACTIVATABLE is narrowest of all — GameFlow.RecordReactionSkip
    /// ignores it, so it silences nothing on the host at all.
    /// </summary>
    private static ReactionSkipScope Narrower(ReactionSkipScope a, ReactionSkipScope b)
    {
        if (a == ReactionSkipScope.NONE) return b;
        if (b == ReactionSkipScope.NONE) return a;
        if (a == ReactionSkipScope.UNTIL_ACTIVATABLE || b == ReactionSkipScope.UNTIL_ACTIVATABLE)
            return ReactionSkipScope.UNTIL_ACTIVATABLE;
        if (a == ReactionSkipScope.TURN_STEP || b == ReactionSkipScope.TURN_STEP)
            return ReactionSkipScope.TURN_STEP;
        return a;
    }

    public class SelectCountryRequestHandler : InputRequest
    {
        public SelectCountryRequestHandler(Faction targetFaction, List<int> targetCountryIds) : base(targetFaction)
        {
            TargetCountryIds = targetCountryIds;
        }

        public override async Task Handle()
        {
            // TargetFaction so the handler can mark this faction's own units standing on the offered
            // countries — a country it already occupies is a legal deploy target, and the marker for
            // that goes on the unit. See SelectCountryHandler.RebuildTargetUnitIds.
            int countryId = await new SelectCountryHandler(TargetCountryIds, TargetFaction).Handle();
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
        // The play-step activations are added to the DISPLAY set only, so one that cannot be used right
        // now — cost unpayable, no legal target, or the play already spent — is drawn greyed out beside
        // the hand instead of silently disappearing from the prompt. Same treatment the hand's own
        // unplayable cards get, and the same reasoning as ReactionWindowDisplayCardIds.
        public override void PopulateTargets()
        {
            DeckState deck = DeckState.ForFaction(TargetFaction);
            TargetCardIds ??= deck.ActivatableCardIds;
            DisplayCardIds ??= deck.ActivatableCardIds
                .Concat(deck.HandCardIds)
                .Concat(CardPlayRound.PlayStepActivationCardIds(TargetFaction))
                .Distinct()
                .ToList();
            PopulateCardTargetPreviews();
        }

        public override async Task Handle()
        {
            // The host's list, not a local re-derivation — same rule as BlockReactionRequestHandler.
            //
            // separateNonHandCards: this is the one prompt whose offer spans two zones, so the table
            // cards that activate instead of a hand play get their own smaller fan beside the hand
            // rather than being interleaved into it by card id.
            // isHandPlayPrompt: tells the bottom-left card back that this prompt IS this faction's hand,
            // so pressing it brings the prompt back instead of browsing an unclickable copy over it.
            // passCostText: this is the one prompt where passing costs something, and the Skip button
            // has to say so before it is pressed.
            PlayerScene.Current.InputManager.SetCardSelectionActive(
                TargetFaction, TargetCardIds ?? new List<int>(), false, DisplayCardIds,
                separateNonHandCards: true, cardTargetPreviews: CardTargetPreviews,
                isHandPlayPrompt: true, passCostText: PassCostText);
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
        {
            // A merged reaction window's offer is the UNION of its factions' options and only the host
            // can build it, so falling back to one faction's activatable cards here would quietly drop
            // the rest of the group rather than fail. The fallback stays for the single-faction case.
            if (TargetCardIds == null && Answering.Count > 1)
                throw new InvalidOperationException(
                    $"A merged reaction window for {string.Join(", ", Answering)} arrived with no " +
                    "TargetCardIds — CardPlayRound must stamp the union of the group's offers.");

            TargetCardIds ??= DeckState.ForFaction(TargetFaction).ActivatableCardIds;
            PopulateCardTargetPreviews();
        }

        public override async Task Handle()
        {
            if (TryAutoPassArmedReactionWindow()) return;

            // The host's list, not a local re-derivation — same rule as BlockReactionRequestHandler.
            //
            // No separateNonHandCards here, unlike HandCardPlayRequestHandler: everything this prompt
            // offers is already a table card, so splitting it out would leave the hand position empty.
            // A reaction window draws ReactionWindowDisplayCardIds (the Status and Response piles), and
            // outside the play window a hand card cannot be IsActivatable at all —
            // CardLogic._defaultPlayConditions requires PLAY_CARD, this faction's turn, and nothing
            // played yet, which is exactly when RequestPlay sends the hand-play request instead.
            PlayerScene.Current.InputManager.SetCardSelectionActive(
                TargetFaction, TargetCardIds ?? new List<int>(), IsReactionWindow, DisplayCardIds,
                cardTargetPreviews: CardTargetPreviews, triggerKind: TriggerReactionKind,
                answeringFactions: Answering);
            if (TriggerCardId > -1)
                TriggerContextDisplay.Current?.ShowCard(
                    TriggerCardId, TriggerSummaryText, TriggerReactionKind, TriggerCauseText,
                    TriggerTargetCountryIds, TriggerTargetUnitIds);
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
    /// Pick one of the offered cards.
    ///
    /// <see cref="CardsRequestHandler"/> is the closest existing prompt and is wrong twice over for a
    /// choice that is not a discard: its title is hardcoded to "Select card(s) to discard", and it
    /// leaves MaxSelections unlimited, so a player asked to take one card could take several. This
    /// clamps the selection to a single card and takes the caller's own wording.
    ///
    /// Required by default, so the modal has no Cancel; pass required: false for a genuinely optional
    /// pick, where an empty ResponseCardIds means "took none".
    /// </summary>
    public class SelectCardRequestHandler : InputRequest
    {
        public string Title { get; set; }
        public bool Required { get; set; }

        public SelectCardRequestHandler(Faction targetFaction, List<int> targetCardIds, string title,
            bool required = true) : base(targetFaction)
        {
            TargetCardIds = targetCardIds;
            Title = title;
            Required = required;
        }

        public override async Task Handle()
        {
            PlayerActionLabel.ShowText(Title, TargetFaction);
            ModalResult result = await ModalStack.Current.Show(ModalConfig.SelectOne(
                Title, PresentationItemCard.FromCardIds(TargetCardIds, true), Required));

            // WasSkipped is deliberately not set: BroadCast turns that into a StepSkippedException,
            // and the caller decides for itself what an empty answer means.
            ResponseCardIds = result.WasCancelled ? new List<int>() : result.SelectedItems;
        }
    }

    /// <summary>
    /// Pick between <see cref="MinSelections"/> and <see cref="MaxSelections"/> of the offered cards,
    /// under the caller's own wording.
    ///
    /// The range is the reason this exists next to <see cref="SelectCardRequestHandler"/>, which clamps
    /// to exactly one, and to <see cref="CardsRequestHandler"/>, whose title says "discard" and whose
    /// upper bound is unlimited. A card that says "take 1 or 2 cards" (StatusRosietheRiveter) is neither.
    ///
    /// MinSelections 0 makes the pick optional: an empty ResponseCardIds means "took none". As with
    /// SelectCardRequestHandler, WasSkipped is never set, so the caller — not BroadCast — decides what
    /// an empty answer means.
    /// </summary>
    public class SelectCardsRequestHandler : InputRequest
    {
        public string Title { get; set; }
        public int MinSelections { get; set; }
        public int MaxSelections { get; set; }

        public SelectCardsRequestHandler(Faction targetFaction, List<int> targetCardIds, string title,
            int minSelections, int maxSelections) : base(targetFaction)
        {
            TargetCardIds = targetCardIds;
            Title = title;
            MinSelections = minSelections;
            MaxSelections = maxSelections;
        }

        public override async Task Handle()
        {
            PlayerActionLabel.ShowText(Title, TargetFaction);
            ModalResult result = await ModalStack.Current.Show(ModalConfig.SelectMany(
                Title, PresentationItemCard.FromCardIds(TargetCardIds, true), MinSelections, MaxSelections));

            ResponseCardIds = result.WasCancelled ? new List<int>() : result.SelectedItems;
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
        /// <summary>
        /// False for a mandatory selection — the answering peer gets no Skip button, and
        /// InputRequestSpec reports PassMode.NotAllowed so the CLI and headless providers refuse a
        /// pass. Serialized with the request: only the target peer's copy runs Handle(), so the flag
        /// has to travel. Still a single parameterized constructor, so System.Text.Json binds it
        /// without a [JsonConstructor].
        ///
        /// A skip can STILL arrive: the host's input-timeout Retry/Skip decision and
        /// ErrorReporter.CancelPendingAwaiters both resolve a request as skipped, and BroadCast turns
        /// that into StepSkippedException. Callers must tolerate it.
        /// </summary>
        public bool AllowSkip { get; set; } = true;

        public SelectUnitRequestHandler(Faction targetFaction, List<int> targetUnitIds, bool allowSkip = true) : base(targetFaction)
        {
            TargetUnitIds = targetUnitIds;
            AllowSkip = allowSkip;
        }

        public override async Task Handle()
        {
            int unitId = await new SelectUnitHandler(TargetUnitIds, AllowSkip).Handle();
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
            // Set here rather than at the one call site for the same reason as IsReactionWindow above:
            // every block prompt is a block window, so the kind is a property of the type.
            TriggerReactionKind = TriggerKind.BLOCK;
        }

        // TargetCardIds/DisplayCardIds are stamped by CardPlayRound.RequestBlock — a block window
        // offers only the block-eligible cards and cannot be re-derived here. Previews still need
        // filling in, and this runs with CurrentBlockTrigger live.
        public override void PopulateTargets() => PopulateCardTargetPreviews();

        public override async Task Handle()
        {
            // Before the delay as well as before the UI — an auto-passed window should not stall the
            // host for the prompt-pacing delay of a prompt nobody is going to see.
            if (TryAutoPassArmedReactionWindow()) return;

            await Task.Delay(GameSettings.DurationMedium);
            // Only the block-eligible cards the host sent — not every activatable card — may be chosen here.
            PlayerScene.Current.InputManager.SetCardSelectionActive(
                TargetFaction, TargetCardIds ?? new List<int>(), IsReactionWindow, DisplayCardIds,
                cardTargetPreviews: CardTargetPreviews, triggerKind: TriggerReactionKind,
                answeringFactions: Answering);
            if (TriggerCardId > -1)
                TriggerContextDisplay.Current?.ShowCard(
                    TriggerCardId, TriggerSummaryText, TriggerReactionKind, TriggerCauseText,
                    TriggerTargetCountryIds, TriggerTargetUnitIds);
            Variant[] results = await AwaitCardSelection();
            ReactionSkipScope = PlayerScene.Current.InputManager.TakeReactionSkipScope();
            if (results != null && results.Length > 0 && (int)results[0] > -1)
            {
                ResponseCardIds.Add((int)results[0]);
            }
        }
    }


    
}
