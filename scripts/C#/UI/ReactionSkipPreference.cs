using System.Collections.Generic;

/// <summary>
/// This peer's per-faction "how much do you want to be asked about reactions" setting, armed ahead of
/// any prompt from the toggle in the faction info row (<see cref="ReactionSkipToggleButton"/>) and
/// also written back whenever a scope is chosen on a live prompt (<see cref="ReactionSkipScopeButton"/>),
/// so the two can never disagree.
///
/// Client-local: nothing here crosses the wire. The only thing that reaches the host is the existing
/// <see cref="InputRequest.ReactionSkipScope"/> field on the prompt's response, and only when a
/// window is actually answered. There is no synchronizer property and no GameMessage.
///
/// <para>
/// <b>Expiry mirrors the host exactly.</b> <see cref="GameFlow"/> keeps its own self-expiring
/// registries (<c>reactionSkipTurnStep</c> / <c>reactionSkipRound</c>) and this reproduces both their
/// rules and <see cref="GameFlow.DropOwnTurnReactionSkip"/>. It can do that safely because every
/// input it reads — GameTurn, TurnStep, and Round/CurrentFaction derived from GameTurn — is on the
/// GameFlowMultiplayerSynchronizer, so a client's answer cannot drift from the host's.
/// </para>
/// </summary>
public static class ReactionSkipPreference
{
    /// <summary>A choice plus the window it was made in, so it can expire the way the host's does.</summary>
    private readonly record struct Armed(ReactionSkipScope Scope, int Turn, TurnStep Step, int Round);

    private static readonly Dictionary<Faction, Armed> Armings = new();

    /// <summary>The cycle order the row toggle walks, least to most silencing, and back to NONE.</summary>
    private static readonly ReactionSkipScope[] CycleOrder =
    {
        ReactionSkipScope.NONE,
        ReactionSkipScope.TURN_STEP,
        ReactionSkipScope.ROUND,
        ReactionSkipScope.UNTIL_ACTIVATABLE,
    };

    /// <summary>
    /// The faction's setting right now, or NONE if it was never armed or has since expired. Expired
    /// entries are dropped on read rather than swept — there is no clearing hook to keep in step with
    /// the turn loop, the same reasoning as the host's registries.
    /// </summary>
    public static ReactionSkipScope ActiveScope(Faction faction)
    {
        if (!Armings.TryGetValue(faction, out Armed armed)) return ReactionSkipScope.NONE;

        if (IsStillActive(faction, armed)) return armed.Scope;

        Armings.Remove(faction);
        return ReactionSkipScope.NONE;
    }

    /// <summary>Arm (or clear, with NONE) a faction's setting and repaint anything showing it.</summary>
    public static void Set(Faction faction, ReactionSkipScope scope)
    {
        if (scope == ReactionSkipScope.NONE)
        {
            Armings.Remove(faction);
        }
        else if (GameFlow.Instance is GameFlow gameFlow)
        {
            Armings[faction] = new Armed(scope, gameFlow.GameTurn, gameFlow.TurnStep, gameFlow.Round);
        }

        EventBus.Emit(EventBus.SignalName.ReactionSkipPreferenceChanged, (int)faction);
    }

    /// <summary>Advance to the next setting in <see cref="CycleOrder"/> and return it.</summary>
    public static ReactionSkipScope Cycle(Faction faction)
    {
        ReactionSkipScope current = ActiveScope(faction);
        int index = System.Array.IndexOf(CycleOrder, current);
        ReactionSkipScope next = CycleOrder[(index + 1) % CycleOrder.Length];
        Set(faction, next);
        return next;
    }

    /// <summary>Forget every arming. For a game ending or a session being torn down.</summary>
    public static void ClearAll() => Armings.Clear();

    private static bool IsStillActive(Faction faction, Armed armed)
    {
        GameFlow gameFlow = GameFlow.Instance;
        if (gameFlow == null) return false;

        switch (armed.Scope)
        {
            case ReactionSkipScope.TURN_STEP:
                return armed.Turn == gameFlow.GameTurn && armed.Step == gameFlow.TurnStep;

            case ReactionSkipScope.ROUND:
                if (armed.Round != gameFlow.Round) return false;
                // GameFlow.DropOwnTurnReactionSkip, mirrored precisely. The host drops a round skip
                // at the moment the faction's OWN turn starts, so that reacting to your own battle
                // (Destroyer Transport, Surprise Attack) is never silently muted by a decision taken
                // on somebody else's turn. It does not ban the scope during your own turn: an arming
                // made after the transition survives, because Drop has already run by then.
                //
                // Testing CurrentFaction alone was stricter than the host and wrong twice over — the
                // scope could not be set on your own turn at all, and because the toggle collapsed
                // straight back to NONE the cycle stalled there and never reached UNTIL_ACTIVATABLE.
                return armed.Turn == gameFlow.GameTurn || gameFlow.CurrentFaction != faction;

            case ReactionSkipScope.UNTIL_ACTIVATABLE:
                // A standing preference. It costs the player nothing to leave on — a window with real
                // options still reaches them — so there is no window for it to expire at the end of.
                return true;

            default:
                return false;
        }
    }
}
