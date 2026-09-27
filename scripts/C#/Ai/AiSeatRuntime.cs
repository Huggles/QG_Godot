using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Installs the bots for this session's AI seats, and owns the input-provider slot while they play.
///
/// Structurally the twin of <see cref="TutorialRuntime"/>'s install/restore half, and deliberately so:
/// that class has been taking over <see cref="InputServices"/> in live GUI games for a while, and the
/// discipline it settled on — remember what was displaced, restore rather than clear, stand
/// everything down on a throw — is exactly what is needed here for the same reasons.
///
/// Host-only. A bot is answered inside the host's process (see
/// <see cref="PlayerFactionRegistry.GetAnsweringPeerForFaction"/>), so a client has no bots to run and
/// gets no provider installed at all — an inert wrapper between InputRequest.Execute and Handle()
/// could only ever be wrong, and it would turn "why did my click do nothing" into a two-layer
/// question.
/// </summary>
public static class AiSeatRuntime
{
    /// <summary>The live installation, or null when this session has no bots on this peer.</summary>
    public static AiSeatInstallation Current { get; private set; }

    /// <summary>
    /// Called from MultiplayerSession.StartSession once the registry is populated, and ABOVE the
    /// restore branch — a loaded save has AI seats exactly like a fresh game does, so installing
    /// inside that branch's else would leave a restored game with the bots silently absent.
    ///
    /// A no-op unless this peer actually answers for a bot, so it is safe to call unconditionally.
    /// </summary>
    public static void InstallIfRequested()
    {
        List<Faction> aiFactions = PlayerFactionRegistry.GetAiFactions();
        if (aiFactions.Count == 0) return;

        // Only the peer that will be ASKED can answer, and for an AI seat that is always the host.
        if (PlayerFactionRegistry.GetLocalPeerId() != PlayerFactionRegistry.HostPeerId) return;

        // Refuse rather than layer. Install order happens to make shadowing work — we go in first, so
        // the tutorial displaces us and hands the slot back correctly — but relying on that is silent,
        // and a script driving five factions while bots are configured for three of them is a
        // configuration mistake rather than a mode. Same for a CLI process, which owns this seam for
        // its whole life and installs the sim bot over everything itself.
        if (!string.IsNullOrEmpty(TutorialRuntime.PendingScriptPath))
        {
            DebugUtilities.PrintPeerErrorRaw(
                "AI seats not installed: a tutorial script is armed for this session, and a script and " +
                "bots cannot both drive the same factions. Playing without AI seats.");
            return;
        }
        // Informational, unlike the tutorial refusal above, because this combination is not a mistake:
        // `cli=true ai_factions=...` is the documented way to exercise the SEAT machinery — the
        // synthetic ids, the extra PlayerScene, the readiness barrier, the TargetPeer translation —
        // with an assertable transcript and no GUI. Declining to also install bots is the point, not a
        // problem. PrintPeerErrorRaw would be doubly wrong here: it routes through GD.PushError, which
        // staples a full C# backtrace to every call, so an expected message would dominate the log.
        if (GameContext.HasScriptedInput)
        {
            DebugUtilities.PrintPeer(
                "AI seats registered but no bots installed: this process answers input from a script " +
                "or the CLI, which owns the input seam for its whole life.");
            return;
        }

        try
        {
            AiSeatInstallation installation = new() { DisplacedProvider = InputServices.Provider };

            foreach (Faction faction in aiFactions)
            {
                PlayerScene seat = PlayerFactionRegistry.GetPlayerSceneForFaction(faction);
                if (seat == null || !seat.IsAiSeat) continue;

                // One bot per SEAT, not per faction: a seat holding two factions plays both from one
                // decision stream, which is what a person holding two factions also does. Already
                // built seats are skipped so the second faction of such a seat does not replace it.
                seat.Bot ??= BuildBot(seat);
                installation.Seats.Add(seat);
            }

            Current = installation;
            InputServices.Override(new AiSeatInputProvider());

            DebugUtilities.PrintPeer(
                $"AI seats installed for {string.Join(", ", aiFactions)} " +
                $"({installation.Seats.Count} seat(s))");
        }
        catch (Exception e)
        {
            // A failure here must not leave the slot half-taken. Through Reset() rather than
            // Override(null) so a throw before the override went in cannot drop somebody else's
            // provider — the game then plays with every seat human, which is never a worse outcome
            // than refusing to start.
            Reset();
            ErrorReporter.Report(e, "AiSeatRuntime.InstallIfRequested");
        }
    }

    /// <summary>
    /// The rules an AI seat plays with, in <c>bot_rules</c> syntax.
    ///
    /// Spelled out here rather than left to <see cref="IBotRule.EnabledByDefault"/>, because the
    /// registry's defaults answer a different question than this one. A rule defaults OFF until an A/B
    /// run says it improves the MEASURED game — that discipline protects the sim baseline, where an
    /// unannounced default flip silently re-answers every balance question asked before it. An opponent
    /// a person is sitting across from is not a measurement: it should play as well as it knows how,
    /// and a rule that is off here is simply absent, with no counter and no trace to say so. Leaving
    /// this null is what made a seat with two deploy rules written, registered and tested still rebuild
    /// its own unit in place instead of taking the empty supply star next door.
    ///
    /// A POSITIVE list, so it switches off every rule it does not name — <c>no_hollow</c> is listed
    /// even though it is default-on, and a rule added to the registry plays for the sim before it plays
    /// here. That is the intended direction: measure first, then name it here. See
    /// <see cref="BotRuleRegistry.Parse"/>.
    /// </summary>
    private const string SeatRules = "no_hollow,avoid_empty_battle,avoid_dead_battle_card,prefer_vacant_deploy," +
                                     "prefer_supply_star_deploy,prefer_valuable_card,prefer_valuable_target";

    /// <summary>
    /// The bot for one seat.
    ///
    /// Uses <see cref="SeatRules"/> with a wall-clock seed, and passes each seat its
    /// <see cref="BotProfile"/> — the per-seat configuration this shape was built to allow.
    ///
    /// The profile changes nothing a player can see yet. It gives the seat a <see cref="BotAgenda"/>,
    /// which is rebuilt from the board on every prompt and which no rule consults, so the seat still
    /// plays purely on SeatRules. It is wired anyway because the alternative — building the agent only in
    /// the headless sim — would leave the live path untested until the first rule depended on it, and the
    /// costs are small and bounded: the agenda is an O(countries + units) read, and
    /// <see cref="BotAgenda.Build"/> is documented never to throw, so a seat cannot be stalled by a
    /// perception bug.
    ///
    /// yieldEvery is small and non-zero rather than the CLI's 64: the empty-reaction-window path
    /// deliberately awaits nothing at all, and a long reaction chain is exactly where those pile up
    /// into one synchronous run with nothing to break it.
    /// </summary>
    private static IInputProvider BuildBot(PlayerScene seat)
    {
        List<IBotRule> rules = BotRuleRegistry.All();

        // Validated here as well as in the CLI runner: a rule whose Kinds name a prompt type that does
        // not exist loads, reports itself enabled, and is then never consulted — indistinguishable at
        // the table from a bot that simply had nothing to say.
        string error = BotRuleRegistry.ValidateKinds(rules);
        Dictionary<string, BotRuleConfig> config = error != null
            ? null
            : BotRuleRegistry.Parse(SeatRules, rules, out error);

        if (config == null)
        {
            // Reachable only when SeatRules names a rule that has since been renamed or removed —
            // which is exactly when failing loudly beats a seat that quietly plays at random.
            throw new InvalidOperationException($"AI seat rule configuration rejected: {error}");
        }

        // Independent of GameRandom by construction, so a bot's decisions can never perturb the deal
        // a seed is supposed to reproduce. Distinct per seat so two bots do not play in lockstep.
        int decisionSeed = Environment.TickCount + seat.GetMultiplayerAuthority();

        return new RandomInputProvider(
            decisionSeed,
            passChance: 0,
            discardChance: 0,
            rules,
            config,
            tierWidth: 0,
            yieldEvery: 16,
            trace: null,
            profileFor: BotProfile.ForFaction);
    }

    /// <summary>
    /// Clear AI-seat state left over from a previous session. Called from SceneFlow on teardown.
    ///
    /// The override is dropped only when we installed one — a CLI session owns that same seam for the
    /// life of its process, so clearing unconditionally would leave a headless run unable to answer
    /// anything. Same rule, same reason, as TutorialRuntime.Reset.
    /// </summary>
    public static void Reset()
    {
        if (Current != null) InputServices.Override(Current.DisplacedProvider);
        Current = null;
    }

    /// <summary>What was installed, so it can be stood back down.</summary>
    public sealed class AiSeatInstallation
    {
        public IInputProvider DisplacedProvider;
        public List<PlayerScene> Seats { get; } = new();
    }
}
