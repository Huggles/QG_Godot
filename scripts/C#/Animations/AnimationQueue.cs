using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class AnimationQueue : SingletonNode<AnimationQueue>
{

    [Signal] public delegate void QueueEmptyEventHandler();
    private readonly Queue<ChangeEventAnimation> _queue = new();
    private bool _isProcessing = false;

    public Task Enqueue(ChangeEventAnimation animation)
    {
        _queue.Enqueue(animation);
        return animation.CompletionTask;
    }

    public async Task Start() {
        if (_queue.Count == 0)
            return;
        if (!_isProcessing)
            _ = ProcessQueue();
        if (_queue.Count == 0) // Check again if all animations were non blocking
            return;
        await ToSignal(this, SignalName.QueueEmpty);        
    }


    public Task EnqueueAndAwait(ChangeEventAnimation animation)
    {
        _queue.Enqueue(animation);
        if (!_isProcessing)
            _ = ProcessQueue();
        return animation.CompletionTask;
    }

    private async Task ProcessQueue()
    {
        _isProcessing = true;
        while (_queue.Count > 0)
        {
            ChangeEventAnimation next = _queue.Dequeue();
            if (next.BlockQueue)
            {
                await next.Execute();
                next.Complete();
            }
            else
            {
                // Fire and forget — queue moves on immediately, animation completes on its own.
                _ = next.Execute().ContinueWith(_ => next.Complete());
            }
        }
        _isProcessing = false;
        EmitSignal(SignalName.QueueEmpty);
    }
}
