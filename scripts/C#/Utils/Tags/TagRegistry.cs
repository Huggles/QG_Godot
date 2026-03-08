using Godot;
using System;
using System.Collections.Generic;

public partial class TagRegistry : Node
{
    Dictionary<Tag, HashSet<StateObject>> registry = new();
    public void RegisterEntity(StateObject e)
    {
        e.Tags.TagAdded += tag => registry[tag].Add(e);
        e.Tags.TagRemoved += tag => registry[tag].Remove(e);
    }
}
