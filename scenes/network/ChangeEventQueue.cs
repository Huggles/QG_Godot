using Godot;
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
            _ = ProcessQueue();
    }

    private async Task ProcessQueue()
    {
        _isProcessing = true;
        while (_queue.Count > 0)
        {
            ChangeEvent next = _queue.Dequeue();
            await next.ApplyChange();
            EmitSignal(SignalName.ChangeEventApplied, next.Id);
        }
        _isProcessing = false;
        EmitSignal(SignalName.QueueDrained);
    }
}
