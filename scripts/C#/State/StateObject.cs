using Godot;
using System;

public partial class StateObject : Node, ITaggable
{
    [Export] public int Id { get; set; }
    private readonly TagContainer _tags = new();
    public TagContainer Tags => _tags;
}
