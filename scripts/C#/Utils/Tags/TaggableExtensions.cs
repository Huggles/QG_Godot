using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Extension methods for ITaggable objects and collections.
/// Makes querying and mutating tags more concise.
/// </summary>
public static class TaggableExtensions
{
    // ── Single-object extensions ───────────────────────────────────────────────

    public static bool HasTag(this ITaggable item, Tag tag, Faction faction) =>
        item.Tags.Has(tag, faction);

    public static bool HasTagForAny(this ITaggable item, Tag tag) =>
        item.Tags.HasForAny(tag);

    public static void AddTag(this ITaggable item, Tag tag, Faction faction) =>
        item.Tags.Add(tag, faction);

    public static void AddTagForAll(this ITaggable item, Tag tag) =>
        item.Tags.AddForAll(tag);

    public static void RemoveTag(this ITaggable item, Tag tag, Faction faction) =>
        item.Tags.Remove(tag, faction);

    public static void RemoveTagForAll(this ITaggable item, Tag tag) =>
        item.Tags.RemoveForAll(tag);

    // ── Collection extensions ──────────────────────────────────────────────────

    /// <summary>
    /// Filters to items that have the specified tag for a specific faction.
    /// </summary>
    public static IEnumerable<T> WithTag<T>(this IEnumerable<T> source, Tag tag, Faction faction) 
        where T : ITaggable
    {
        return source.Where(item => item.Tags.Has(tag, faction));
    }

    /// <summary>
    /// Filters to items that have the specified tag for ANY faction.
    /// </summary>
    public static IEnumerable<T> WithTagAnyFaction<T>(this IEnumerable<T> source, Tag tag) 
        where T : ITaggable
    {
        return source.Where(item => item.Tags.HasForAny(tag));
    }

    /// <summary>
    /// Filters to items that have ALL of the specified tags for a faction.
    /// </summary>
    public static IEnumerable<T> WithAllTags<T>(this IEnumerable<T> source, Faction faction, params Tag[] tags) 
        where T : ITaggable
    {
        return source.Where(item => item.Tags.HasAll(faction, tags));
    }

    /// <summary>
    /// Filters to items that have ANY of the specified tags for a faction.
    /// </summary>
    public static IEnumerable<T> WithAnyTag<T>(this IEnumerable<T> source, Faction faction, params Tag[] tags) 
        where T : ITaggable
    {
        return source.Where(item => item.Tags.HasAny(faction, tags));
    }

    /// <summary>
    /// Filters to items that do NOT have the specified tag for a faction.
    /// </summary>
    public static IEnumerable<T> WithoutTag<T>(this IEnumerable<T> source, Tag tag, Faction faction) 
        where T : ITaggable
    {
        return source.Where(item => !item.Tags.Has(tag, faction));
    }

    /// <summary>
    /// Filters to items that have NONE of the specified tags for a faction.
    /// </summary>
    public static IEnumerable<T> WithoutAnyTag<T>(this IEnumerable<T> source, Faction faction, params Tag[] tags) 
        where T : ITaggable
    {
        return source.Where(item => item.Tags.HasNone(faction, tags));
    }

    /// <summary>
    /// Adds a tag to all items in the collection for a specific faction.
    /// </summary>
    public static void AddTag<T>(this IEnumerable<T> source, Tag tag, Faction faction) 
        where T : ITaggable
    {
        foreach (var item in source)
        {
            item.Tags.Add(tag, faction);
        }
    }

    /// <summary>
    /// Adds a tag to all items in the collection for all factions.
    /// </summary>
    public static void AddTagForAll<T>(this IEnumerable<T> source, Tag tag) 
        where T : ITaggable
    {
        foreach (var item in source)
        {
            item.Tags.AddForAll(tag);
        }
    }

    /// <summary>
    /// Removes a tag from all items in the collection for a specific faction.
    /// </summary>
    public static void RemoveTag<T>(this IEnumerable<T> source, Tag tag, Faction faction) 
        where T : ITaggable
    {
        foreach (var item in source)
        {
            item.Tags.Remove(tag, faction);
        }
    }

    /// <summary>
    /// Removes a tag from all items in the collection for all factions.
    /// </summary>
    public static void RemoveTagForAll<T>(this IEnumerable<T> source, Tag tag) 
        where T : ITaggable
    {
        foreach (var item in source)
        {
            item.Tags.RemoveForAll(tag);
        }
    }

    /// <summary>
    /// Gets the count of items with the specified tag for a faction.
    /// </summary>
    public static int CountWithTag<T>(this IEnumerable<T> source, Tag tag, Faction faction) 
        where T : ITaggable
    {
        return source.Count(item => item.Tags.Has(tag, faction));
    }

    /// <summary>
    /// Checks if any item in the collection has the specified tag for a faction.
    /// </summary>
    public static bool AnyWithTag<T>(this IEnumerable<T> source, Tag tag, Faction faction) 
        where T : ITaggable
    {
        return source.Any(item => item.Tags.Has(tag, faction));
    }

    /// <summary>
    /// Checks if all items in the collection have the specified tag for a faction.
    /// </summary>
    public static bool AllWithTag<T>(this IEnumerable<T> source, Tag tag, Faction faction) 
        where T : ITaggable
    {
        return source.All(item => item.Tags.Has(tag, faction));
    }

    /// <summary>
    /// Gets the first item with the specified tag for a faction, or null if not found.
    /// </summary>
    public static T FirstOrDefaultWithTag<T>(this IEnumerable<T> source, Tag tag, Faction faction) 
        where T : class, ITaggable
    {
        return source.FirstOrDefault(item => item.Tags.Has(tag, faction));
    }
}
