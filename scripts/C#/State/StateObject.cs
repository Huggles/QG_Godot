using Godot;
using System;
using System.Text.Json.Serialization;

public partial class StateObject : ITaggable
{
    [Export] public int Id { get; set; }
    [JsonIgnore] private TagContainer _ownTags;

    /// <summary>A board object's tags live in its <see cref="BoardRecord"/>, and it overrides this to say so.</summary>
    [JsonIgnore] public virtual TagContainer Tags => _ownTags ??= new();
}
