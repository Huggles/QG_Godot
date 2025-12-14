using Godot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class GameQueue<T> where T : IGameQueueItem
{
    private readonly Queue<T> _queue = new();

    public bool IsExecuting => _isExecuting;
    private bool _isExecuting = false;
    public void Enqueue(T item)
    {
        if (item == null)
        {
            GD.PrintErr("[GameFlowQueue] Tried to enqueue null item.");
            return;
        }

        _queue.Enqueue(item);

        // Start processing if not already doing so
        if (!_isExecuting)
        {
            _ = ProcessQueueAsync();
        }
            
    }

    public void Clear()
    {
        _queue.Clear();
    }

    /// <summary>
    /// Returns true if an item is currently being processed.
    /// </summary>
    

    private async Task ProcessQueueAsync()
    {
        _isExecuting = true;
        while (_queue.Count > 0)
        {
            T current = _queue.Dequeue();

            try
            {
                await current.Execute();
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"[GameFlowQueue] Error executing item: {ex}");
            }
        }
        _isExecuting = false;
    }
}
