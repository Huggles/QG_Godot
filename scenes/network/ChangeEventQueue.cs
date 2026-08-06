using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class ChangeEventQueue : SingletonNode<ChangeEventQueue>
{
    [Signal] public delegate void ChangeEventAppliedEventHandler(int changeEventId);
    [Signal] public delegate void QueueDrainedEventHandler();

    private readonly Queue<ChangeEvent> _queue = new();
    private bool _isProcessing = false;

    public bool IsIdle => !_isProcessing && _queue.Count == 0;

    public void Enqueue(ChangeEvent changeEvent)
    {
        _queue.Enqueue(changeEvent);
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
                ChangeEvent next = _queue.Dequeue();
                try
                {
                    ErrorInjection.MaybeThrow(ErrorInjection.Site.ChangeEventQueue, next?.ScriptName);
                    await next.ApplyChange();
                    EmitSignal(SignalName.ChangeEventApplied, next.Id);
                }
                catch (Exception e)
                {
                    // Per-item, deliberately: dropping one event is a desync the hash check will
                    // catch, but letting the throw escape strands the pump (see the finally below)
                    // and the client stops applying ChangeEvents entirely — a hard freeze.
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
