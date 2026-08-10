using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// The serial pump that applies replicated <see cref="GameMessage"/>s in wire order on a client.
///
/// This queue — not any one message type — is the ordering primitive of the replicated stream. An Rpc
/// body runs immediately in the receiving frame, while everything enqueued here is deferred, so a raw
/// Rpc always risks landing ahead of the messages it belongs with. Anything that needs a defined
/// position in the stream has to ride through here, which is why presentation-only broadcasts are
/// PresentationEvents rather than bare Rpcs.
/// </summary>
public partial class ChangeEventQueue : SingletonNode<ChangeEventQueue>
{
    [Signal] public delegate void QueueDrainedEventHandler();

    private readonly Queue<GameMessage> _queue = new();
    private bool _isProcessing = false;

    public bool IsIdle => !_isProcessing && _queue.Count == 0;

    public void Enqueue(GameMessage message)
    {
        _queue.Enqueue(message);
        if (!_isProcessing)
            Guard.FireAndForget(ProcessQueue, "ChangeEventQueue.ProcessQueue");
    }

    /// <summary>
    /// Completes once the queue is empty. Replaces the
    /// <c>if (!IsIdle) await ToSignal(QueueDrained)</c> idiom, which was a check-then-await race:
    /// if the queue drained between the check and the subscribe, QueueDrained had already fired and
    /// the caller waited forever.
    /// </summary>
    public async Task WhenDrained()
    {
        if (IsIdle) return;
        await ToSignal(this, SignalName.QueueDrained);
    }

    private async Task ProcessQueue()
    {
        _isProcessing = true;
        try
        {
            while (_queue.Count > 0)
            {
                GameMessage next = _queue.Dequeue();
                try
                {
                    ErrorInjection.MaybeThrow(ErrorInjection.Site.ChangeEventQueue, next?.ScriptName);
                    await next.Apply();
                }
                catch (Exception e)
                {
                    // Per-item, deliberately: letting the throw escape strands the pump (see the
                    // finally below) and the client stops applying messages entirely — a hard freeze.
                    // For a ChangeEvent, dropping one is a desync the hash check will catch. For a
                    // PresentationEvent there is nothing to catch it, so a dropped one is a silently
                    // missing animation — an accepted cost of keeping the pump alive.
                    ErrorReporter.Report(e, $"ChangeEventQueue applying {next?.ScriptName}",
                        next?.TriggeringFaction);
                }
            }
        }
        finally
        {
            // Must always run. Previously a throw left _isProcessing stuck true, so Enqueue never
            // restarted the pump and every awaiter of QueueDrained hung forever.
            _isProcessing = false;
            EmitSignal(SignalName.QueueDrained);
        }
    }
}
