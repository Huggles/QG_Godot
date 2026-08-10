using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class AnimationQueue : SingletonNode<AnimationQueue>
{

    [Signal] public delegate void QueueEmptyEventHandler();
    private readonly Queue<ChangeEventAnimation> _queue = new();
    private bool _isProcessing = false;

    public bool IsIdle => !_isProcessing && _queue.Count == 0;

    public Task Enqueue(ChangeEventAnimation animation)
    {
        _queue.Enqueue(animation);
        return animation.CompletionTask;
    }

    public async Task Start() {
        if (_queue.Count == 0)
            return;

        // Subscribe BEFORE pumping. Previously this started ProcessQueue and only then awaited
        // QueueEmpty — a short queue could drain and emit the signal before the await subscribed,
        // hanging every ChangeEvent.Apply (which awaits this on every event).
        SignalAwaiter drained = ToSignal(this, SignalName.QueueEmpty);

        if (!_isProcessing)
            Guard.FireAndForget(ProcessQueue, "AnimationQueue.ProcessQueue");

        if (IsIdle) // all animations were non-blocking and already finished
            return;

        await drained;
    }


    public Task EnqueueAndAwait(ChangeEventAnimation animation)
    {
        _queue.Enqueue(animation);
        if (!_isProcessing)
            Guard.FireAndForget(ProcessQueue, "AnimationQueue.ProcessQueue");
        return animation.CompletionTask;
    }

    /// <summary>
    /// Drop every queued animation, completing each one so nothing is left awaiting it. Used by the
    /// error-recovery sweep: a stranded CompletionTask would deadlock the resumed loop.
    /// </summary>
    public void CancelAll()
    {
        int cancelled = _queue.Count;
        while (_queue.Count > 0)
            _queue.Dequeue().Complete();

        _isProcessing = false;
        EmitSignal(SignalName.QueueEmpty);

        if (cancelled > 0)
            DebugUtilities.PrintPeer($"AnimationQueue: cancelled {cancelled} pending animation(s)");
    }

    private async Task ProcessQueue()
    {
        _isProcessing = true;
        try
        {
            while (_queue.Count > 0)
            {
                ChangeEventAnimation next = _queue.Dequeue();
                if (next.BlockQueue)
                {
                    try
                    {
                        await next.Execute();
                    }
                    catch (Exception e)
                    {
                        ErrorReporter.ReportRecovered(e, $"Animation {next?.ScriptName}");
                    }
                    finally
                    {
                        // Must run even on failure, or the CompletionTask handed out by
                        // Enqueue/EnqueueAndAwait never completes and its awaiter hangs forever.
                        next.Complete();
                    }
                }
                else
                {
                    // Fire and forget — queue moves on immediately, animation completes on its own.
                    // ContinueWith also runs on the faulted path, so Complete() is guaranteed; we
                    // read the exception here so it is observed rather than left to the finalizer.
                    _ = next.Execute().ContinueWith(t =>
                    {
                        if (t.IsFaulted && t.Exception != null)
                            ErrorReporter.ReportRecovered(t.Exception.GetBaseException(), $"Animation {next?.ScriptName}");
                        next.Complete();
                    });
                }
            }
        }
        finally
        {
            // Must always run. Previously a throw left _isProcessing stuck true, so the pump never
            // restarted and every awaiter of QueueEmpty — including ChangeEvent.Apply — hung.
            _isProcessing = false;
            EmitSignal(SignalName.QueueEmpty);
        }
    }
}
