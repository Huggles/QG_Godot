using System.Threading.Tasks;

/// <summary>
/// Routes each prompt to whoever owns the seat it is for: the person at the keyboard, or that seat's
/// bot.
///
/// Installed over the whole process by <see cref="AiSeatRuntime"/>, because
/// <see cref="InputServices"/> has exactly one provider slot. That is fine and is not a compromise —
/// <see cref="InputRequest.Execute"/> only reaches a provider when the prompt is for a faction THIS
/// peer answers for, and on the host that set is "my own factions plus every bot". So one object
/// legitimately sees both, and its only job is to tell them apart. TutorialInputProvider has done
/// exactly this in live GUI games for five scripted factions against one human; this is that pattern
/// with the roles swapped.
///
/// Routing goes through the seat object rather than a set of AI factions on purpose: the bot lives on
/// its own <see cref="PlayerScene"/>, so "which bot" and "which configuration" are answered by the
/// same lookup that answers "whose seat is this".
/// </summary>
public sealed class AiSeatInputProvider : IInputProvider
{
    /// <summary>
    /// Where a prompt for a human seat goes.
    ///
    /// Constructed explicitly rather than captured from <see cref="InputServices.Provider"/>, and this
    /// matters: that property falls back to <see cref="AutoPassInputProvider"/> whenever
    /// <c>GameContext.HasScriptedInput</c> is set, so capturing it would mean the person's OWN prompts
    /// silently auto-passing if that flag were ever flipped for this feature. The displaced provider is
    /// remembered by AiSeatRuntime for restore only, never consulted here.
    /// </summary>
    private readonly IInputProvider _human;

    public AiSeatInputProvider()
    {
        _human = GameContext.IsHeadless ? new AutoPassInputProvider() : new GodotInputProvider();
    }

    public async Task Resolve(InputRequest request)
    {
        PlayerScene seat = PlayerFactionRegistry.GetPlayerSceneForFaction(request.TargetFaction);

        // Fails toward the human, deliberately. A missing seat, an invalid node or a bot that was
        // never installed all land here, and handing a person's prompt to a bot is far worse than
        // handing a bot's prompt to the person — who can at least see what happened and answer it.
        if (seat == null || !seat.IsAiSeat || seat.Bot == null)
        {
            await _human.Resolve(request);
            return;
        }

        int think = ThinkTimeMs(request);
        if (think > 0)
        {
            // Borrow the watcher treatment for the pause. From the human's point of view "Waiting on
            // Germany input…" is exactly true, even though the prompt is being answered inside their
            // own process. Only while actually pausing, or it would flicker on every empty reaction
            // window; MarkInputClosed is idempotent, so the AnnounceInputClosed broadcast that follows
            // is harmless.
            InputRequest.MarkInputOpen(request.TargetFaction);
            try
            {
                await Task.Delay(think);
            }
            finally
            {
                InputRequest.MarkInputClosed(request.TargetFaction);
            }
        }

        try
        {
            await seat.Bot.Resolve(request);
        }
        catch (System.Exception e)
        {
            // Reported here, where the seat and the prompt are both known, then rethrown. Without this
            // a bot's failure surfaces as an error inside the ReceiveInputRequest RPC with no mention
            // of the AI at all — and a rules failure during a bot's turn is the single most likely
            // place a real bug in this feature shows up, so it must not be anonymous.
            ErrorReporter.Report(e, $"AI seat answering {request.GetType().Name}", request.TargetFaction);
            ErrorReporter.MarkReported(e);
            throw;
        }
    }

    /// <summary>
    /// How long to let a bot appear to think before it answers.
    ///
    /// Read off <see cref="GameSettings"/>'s duration table rather than a scalar of its own, which
    /// gets three things for free: the player's Slow/Normal/Fast choice already applies, it returns 0
    /// headless so the sim is untouched, and it returns 0 while fast-forwarding a restore so a load
    /// does not re-live every pause.
    ///
    /// Tiered because a flat delay is wrong in both directions:
    ///  - An empty always-ask reaction window has exactly one legal answer and there are dozens a
    ///    turn. Pausing on those would be unbearable, and Handle() already makes this same carve-out
    ///    for its own prompt-pacing delay.
    ///  - A prompt a card step raised (a build target, a battle target) is the continuation of a
    ///    decision the human already watched a pause for. Charging full price again makes one card
    ///    play feel like four.
    ///  - Choosing what to PLAY is the decision worth watching, and it is also the one the turn badge
    ///    is fading over — so it gets the longest pause, which is what makes a bot's turn legible.
    /// </summary>
    private static int ThinkTimeMs(InputRequest request)
    {
        if (request.IsEmptyReactionWindow) return 0;
        if (request is InputRequest.HandCardPlayRequestHandler) return GameSettings.DurationLong;
        if (request.OriginCardId >= 0) return GameSettings.DurationShort;
        return GameSettings.DurationMedium;
    }
}
