using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class TagContainer : Node
{
    private readonly HashSet<Tag> _tags = new();

    public event Action<Tag>? TagAdded;
    public event Action<Tag>? TagRemoved;

    public bool Add(Tag tag)
    {
        if (_tags.Add(tag))
        {
            TagAdded?.Invoke(tag);
            return true;
        }
        return false;
    }

    public bool Remove(Tag tag)
    {
        if (_tags.Remove(tag))
        {
            TagRemoved?.Invoke(tag);
            return true;
        }
        return false;
    }

    public void Clear()
    {
        // avoid modifying collection while iterating
        var toRemove = _tags.ToArray();
        _tags.Clear();
        foreach (var tag in toRemove)
            TagRemoved?.Invoke(tag);
    }

    public bool Has(Tag tag) => _tags.Contains(tag);
}
