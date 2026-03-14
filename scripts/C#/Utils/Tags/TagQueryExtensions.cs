using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Extension methods for querying nodes by tags with faction support.
/// </summary>
public static class TagQueryExtensions
{
    /// <summary>
    /// Gets the TagComponent from a node, if it exists.
    /// </summary>
    public static TagComponent GetTagComponent(this Node node)
    {
        return node.GetNodeOrNull<TagComponent>("TagComponent");
    }

    /// <summary>
    /// Gets the TagContainer from a node (works for both ITaggable objects and nodes with TagComponent).
    /// </summary>
    public static TagContainer GetTags(this Node node)
    {
        // Check if the node itself is ITaggable
        if (node is ITaggable taggable)
        {
            return taggable.Tags;
        }

        // Check for TagComponent child
        var tagComponent = node.GetTagComponent();
        return tagComponent?.Tags;
    }

    /// <summary>
    /// Checks if a node has a specific tag for a specific faction.
    /// </summary>
    public static bool HasTag(this Node node, Tag tag, Faction faction)
    {
        var tags = node.GetTags();
        return tags?.Has(tag, faction) ?? false;
    }

    /// <summary>
    /// Checks if a node has a specific tag for ANY faction.
    /// </summary>
    public static bool HasTagAnyFaction(this Node node, Tag tag)
    {
        var tags = node.GetTags();
        return tags?.HasForAny(tag) ?? false;
    }

    /// <summary>
    /// Checks if a node has all specified tags for a faction.
    /// </summary>
    public static bool HasAllTags(this Node node, Faction faction, params Tag[] tags)
    {
        var container = node.GetTags();
        return container?.HasAll(faction, tags) ?? false;
    }

    /// <summary>
    /// Checks if a node has any of the specified tags for a faction.
    /// </summary>
    public static bool HasAnyTag(this Node node, Faction faction, params Tag[] tags)
    {
        var container = node.GetTags();
        return container?.HasAny(faction, tags) ?? false;
    }

    /// <summary>
    /// Gets all factions that have a specific tag on this node.
    /// </summary>
    public static IEnumerable<Faction> GetFactionsWithTag(this Node node, Tag tag)
    {
        var tags = node.GetTags();
        return tags?.GetFactionsWithTag(tag) ?? Enumerable.Empty<Faction>();
    }

    /// <summary>
    /// Adds a tag to a node for a specific faction (creates TagComponent if needed).
    /// </summary>
    public static void AddTag(this Node node, Tag tag, Faction faction)
    {
        var tags = node.GetTags();
        if (tags != null)
        {
            tags.Add(tag, faction);
        }
        else
        {
            // If node doesn't have tags, add a TagComponent
            var tagComponent = new TagComponent();
            node.AddChild(tagComponent);
            tagComponent.Name = "TagComponent";
            tagComponent.Tags.Add(tag, faction);
        }
    }

    /// <summary>
    /// Adds a tag to a node for all factions (creates TagComponent if needed).
    /// </summary>
    public static void AddTagForAll(this Node node, Tag tag)
    {
        var tags = node.GetTags();
        if (tags != null)
        {
            tags.AddForAll(tag);
        }
        else
        {
            // If node doesn't have tags, add a TagComponent
            var tagComponent = new TagComponent();
            node.AddChild(tagComponent);
            tagComponent.Name = "TagComponent";
            tagComponent.Tags.AddForAll(tag);
        }
    }

    /// <summary>
    /// Removes a tag from a node for a specific faction.
    /// </summary>
    public static void RemoveTag(this Node node, Tag tag, Faction faction)
    {
        var tags = node.GetTags();
        tags?.Remove(tag, faction);
    }

    /// <summary>
    /// Removes a tag from a node for all factions.
    /// </summary>
    public static void RemoveTagForAll(this Node node, Tag tag)
    {
        var tags = node.GetTags();
        tags?.RemoveForAll(tag);
    }

    /// <summary>
    /// Finds all children (recursive) with a specific tag for a specific faction.
    /// </summary>
    public static IEnumerable<Node> FindChildrenWithTag(this Node node, Tag tag, Faction faction, bool recursive = true)
    {
        var results = new List<Node>();
        FindChildrenWithTagRecursive(node, tag, faction, results, recursive);
        return results;
    }

    private static void FindChildrenWithTagRecursive(Node node, Tag tag, Faction faction, List<Node> results, bool recursive)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child.HasTag(tag, faction))
            {
                results.Add(child);
            }

            if (recursive)
            {
                FindChildrenWithTagRecursive(child, tag, faction, results, recursive);
            }
        }
    }

    /// <summary>
    /// Finds all children (recursive) with a specific tag for ANY faction.
    /// </summary>
    public static IEnumerable<Node> FindChildrenWithTagAnyFaction(this Node node, Tag tag, bool recursive = true)
    {
        var results = new List<Node>();
        FindChildrenWithTagAnyFactionRecursive(node, tag, results, recursive);
        return results;
    }

    private static void FindChildrenWithTagAnyFactionRecursive(Node node, Tag tag, List<Node> results, bool recursive)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child.HasTagAnyFaction(tag))
            {
                results.Add(child);
            }

            if (recursive)
            {
                FindChildrenWithTagAnyFactionRecursive(child, tag, results, recursive);
            }
        }
    }

    /// <summary>
    /// Finds all children (recursive) with all specified tags for a faction.
    /// </summary>
    public static IEnumerable<Node> FindChildrenWithAllTags(this Node node, Faction faction, params Tag[] tags)
    {
        var results = new List<Node>();
        FindChildrenWithAllTagsRecursive(node, faction, tags, results);
        return results;
    }

    private static void FindChildrenWithAllTagsRecursive(Node node, Faction faction, Tag[] tags, List<Node> results)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child.HasAllTags(faction, tags))
            {
                results.Add(child);
            }

            FindChildrenWithAllTagsRecursive(child, faction, tags, results);
        }
    }

    /// <summary>
    /// Finds all children (recursive) with any of the specified tags for a faction.
    /// </summary>
    public static IEnumerable<Node> FindChildrenWithAnyTag(this Node node, Faction faction, params Tag[] tags)
    {
        var results = new List<Node>();
        FindChildrenWithAnyTagRecursive(node, faction, tags, results);
        return results;
    }

    private static void FindChildrenWithAnyTagRecursive(Node node, Faction faction, Tag[] tags, List<Node> results)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child.HasAnyTag(faction, tags))
            {
                results.Add(child);
            }

            FindChildrenWithAnyTagRecursive(child, faction, tags, results);
        }
    }

    /// <summary>
    /// Gets the first child with the specified tag for a faction.
    /// </summary>
    public static Node FindFirstChildWithTag(this Node node, Tag tag, Faction faction, bool recursive = true)
    {
        return FindChildrenWithTag(node, tag, faction, recursive).FirstOrDefault();
    }

    /// <summary>
    /// Gets the first child with the specified tag for any faction.
    /// </summary>
    public static Node FindFirstChildWithTagAnyFaction(this Node node, Tag tag, bool recursive = true)
    {
        return FindChildrenWithTagAnyFaction(node, tag, recursive).FirstOrDefault();
    }
}
