using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// A component that can be attached to any Node to give it tag functionality.
/// Add this as a child of any node you want to tag.
/// </summary>
public partial class TagComponent : Node, ITaggable
{
    private readonly TagContainer _tags = new();
    
    public TagContainer Tags => _tags;

    /// <summary>
    /// Tags to add when the component is ready.
    /// Set these in code before _Ready() is called.
    /// </summary>
    public Tag[] InitialTags { get; set; } = Array.Empty<Tag>();

    /// <summary>
    /// The faction to use when adding initial tags.
    /// If set to NONE, InitialTags will be added for ALL factions.
    /// </summary>
    public Faction InitialTagFaction { get; set; } = Faction.NONE;

    public override void _Ready()
    {
        // Add initial tags on ready
        if (InitialTags != null && InitialTags.Length > 0)
        {
            if (InitialTagFaction == Faction.NONE)
            {
                // Add for all factions
                foreach (var tag in InitialTags)
                {
                    _tags.AddForAll(tag);
                }
            }
            else
            {
                // Add for specific faction
                _tags.AddRange(InitialTagFaction, InitialTags);
            }
        }
    }

    /// <summary>
    /// Gets the parent node of this tag component.
    /// </summary>
    public Node GetOwnerNode() => GetParent();
}
