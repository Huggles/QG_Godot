using Godot;
using System;
using System.Text.Json.Serialization;

public partial class StateObject : ITaggable
{
    [Export] public int Id { get; set; }
    [JsonIgnore] private readonly TagContainer _tags = new();
    [JsonIgnore] public TagContainer Tags => _tags;
}
