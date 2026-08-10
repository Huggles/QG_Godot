using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// A message whose entire effect is what the player sees. It rides the same ordered channel as
/// <see cref="ChangeEvent"/> so it lands at a well-defined point relative to the effects it belongs
/// with — that ordering is the only thing it needs from the channel, and it is the only reason a
/// presentation-only broadcast cannot just be a raw Rpc.
///
/// It is a sibling of ChangeEvent rather than a ChangeEvent that changes nothing, and the difference is
/// load-bearing, not cosmetic:
///   • no state hash is stamped or compared, so no ComputeHash() runs on any peer for a message that
///     cannot move state;
///   • it cannot reach CardPlayRound.RegisterChangeEvent, so it can never become LastChangeEvent and
///     silently steer reaction request order or the self-block guard in RequestBlock — the hazard the
///     old RegisterInCardPlayPool hook existed to document is now inexpressible;
///   • no GameStateCalculator.CalculateAll(), so it does not drag a full per-faction tag recalculation
///     and a ComputedTagsSnapshot broadcast along behind it;
///   • the divergence bookkeeping in ChangeEvent never arms, because there is no window between
///     mutating state and telling the clients about it.
///
/// It still gets a stream Id and still appears in the game history: "this was announced" is a narrative
/// fact worth logging, and for a Bulletin the announcement text is the only place a player can re-read
/// why something happened.
/// </summary>
public abstract partial class PresentationEvent : GameMessage
{
    protected PresentationEvent(Faction triggeringFaction) : base(triggeringFaction) { }

    /// <summary>
    /// What the player sees. One list rather than Before/After, because there is no mutation for
    /// animations to sit either side of.
    /// </summary>
    protected abstract List<ChangeEventAnimation> Animations { get; }

    public abstract override PresentationEventDto ToDto();

    protected override async Task<bool> ApplyInternal(int capturedEpoch)
    {
        // Error recovery may have moved the loop on while this sat in the queue. Bail rather than
        // present something the resumed loop is no longer doing.
        ErrorReporter.ThrowIfStaleEpoch(capturedEpoch);

        DebugUtilities.PrintPeer($"Presenting {ScriptName} (Id: {Id}) for {TriggeringFaction}");

        // Broadcast before presenting — the same relative order ChangeEvent uses, and for a stronger
        // reason here: a blocking animation parks this method until the local modal closes, so getting
        // the message onto the wire first means every peer starts its own copy at the same point in the
        // stream instead of after the host's has finished.
        if (IsServer) await BroadCast();

        EnqueueAnimations(() => Animations);
        await PresentationServices.Animation.Start();

        RecordApplied();
        // Feeds GameHistoryList, which listens on this rather than on the queue.
        EventBus.Emit(EventBus.SignalName.GameChangeEventAfter, ScriptName);
        return true;
    }
}
