using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class TagContainer : Node
{
    // Maps each tag to the set of factions it applies to
    private readonly Dictionary<Tag, HashSet<Faction>> _tags = new();

    public event Action<Tag, Faction> TagAdded;
    public event Action<Tag, Faction> TagRemoved;

    /// <summary>
    /// Adds a tag for a specific faction.
    /// </summary>
    public bool Add(Tag tag, Faction faction)
    {
        if (faction == Faction.NONE)
            return false;

        if (!_tags.ContainsKey(tag))
        {
            _tags[tag] = new HashSet<Faction>();
        }

        if (_tags[tag].Add(faction))
        {
            TagAdded?.Invoke(tag, faction);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Adds a tag for all factions.
    /// </summary>
    public void AddForAll(Tag tag)
    {
        Add(tag, Faction.ALL);
    }

    /// <summary>
    /// Removes a tag for a specific faction.
    /// </summary>
    public bool Remove(Tag tag, Faction faction)
    {
        if (!_tags.ContainsKey(tag))
            return false;

        if (_tags[tag].Remove(faction))
        {
            TagRemoved?.Invoke(tag, faction);
            
            // Clean up empty tag entries
            if (_tags[tag].Count == 0)
            {
                _tags.Remove(tag);
            }
            return true;
        }
        return false;
    }

    /// <summary>
    /// Removes a tag for all factions.
    /// </summary>
    public void RemoveForAll(Tag tag)
    {
        if (!_tags.ContainsKey(tag))
            return;

        var factions = _tags[tag].ToArray();
        foreach (var faction in factions)
        {
            Remove(tag, faction);
        }
    }

    /// <summary>
    /// Clears all tags for all factions.
    /// </summary>
    public void Clear()
    {
        var allTags = _tags.Keys.ToArray();
        foreach (var tag in allTags)
        {
            RemoveForAll(tag);
        }
    }

    /// <summary>
    /// Checks if a tag is present for a specific faction.
    /// Also returns true if the tag has Faction.ALL.
    /// </summary>
    public bool Has(Tag tag, Faction faction)
    {
        if (!_tags.ContainsKey(tag))
            return false;

        return _tags[tag].Contains(faction) || _tags[tag].Contains(Faction.ALL);
    }

    /// <summary>
    /// Checks if a tag is present for ANY faction.
    /// </summary>
    public bool HasForAny(Tag tag)
    {
        return _tags.ContainsKey(tag) && _tags[tag].Count > 0;
    }

    /// <summary>
    /// Gets all factions that have a specific tag.
    /// </summary>
    public IEnumerable<Faction> GetFactionsWithTag(Tag tag)
    {
        if (!_tags.ContainsKey(tag))
            return Enumerable.Empty<Faction>();

        return _tags[tag];
    }

    /// <summary>
    /// Checks if all specified tags are present for a faction.
    /// </summary>
    public bool HasAll(Faction faction, params Tag[] tags)
    {
        foreach (var tag in tags)
        {
            if (!Has(tag, faction))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Checks if any of the specified tags are present for a faction.
    /// </summary>
    public bool HasAny(Faction faction, params Tag[] tags)
    {
        foreach (var tag in tags)
        {
            if (Has(tag, faction))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Checks if none of the specified tags are present for a faction.
    /// </summary>
    public bool HasNone(Faction faction, params Tag[] tags)
    {
        return !HasAny(faction, tags);
    }

    /// <summary>
    /// Returns all unique tags in the container (regardless of faction).
    /// </summary>
    public IEnumerable<Tag> GetAllTags() => _tags.Keys;

    /// <summary>
    /// Returns all tags that apply to a specific faction.
    /// </summary>
    public IEnumerable<Tag> GetTagsForFaction(Faction faction)
    {
        return _tags.Where(kvp => kvp.Value.Contains(faction) || kvp.Value.Contains(Faction.ALL))
                    .Select(kvp => kvp.Key);
    }

    /// <summary>
    /// Returns the total number of unique tags.
    /// </summary>
    public int Count => _tags.Count;

    /// <summary>
    /// Adds multiple tags for a specific faction.
    /// </summary>
    public void AddRange(Faction faction, params Tag[] tags)
    {
        foreach (var tag in tags)
        {
            Add(tag, faction);
        }
    }

    /// <summary>
    /// Removes multiple tags for a specific faction.
    /// </summary>
    public void RemoveRange(Faction faction, params Tag[] tags)
    {
        foreach (var tag in tags)
        {
            Remove(tag, faction);
        }
    }
}
