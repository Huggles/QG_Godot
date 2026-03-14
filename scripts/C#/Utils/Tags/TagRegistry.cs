using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class TagRegistry : Node
{
    // Maps (Tag, Faction) pairs to sets of entities
    private readonly Dictionary<(Tag, Faction), HashSet<StateObject>> _registry = new();

    public void RegisterEntity(StateObject entity)
    {
        entity.Tags.TagAdded += (tag, faction) => OnTagAdded(entity, tag, faction);
        entity.Tags.TagRemoved += (tag, faction) => OnTagRemoved(entity, tag, faction);

        // Add existing tags to registry
        foreach (var tag in entity.Tags.GetAllTags())
        {
            foreach (var faction in entity.Tags.GetFactionsWithTag(tag))
            {
                OnTagAdded(entity, tag, faction);
            }
        }
    }

    public void UnregisterEntity(StateObject entity)
    {
        foreach (var tag in entity.Tags.GetAllTags())
        {
            foreach (var faction in entity.Tags.GetFactionsWithTag(tag))
            {
                OnTagRemoved(entity, tag, faction);
            }
        }
    }

    private void OnTagAdded(StateObject entity, Tag tag, Faction faction)
    {
        var key = (tag, faction);
        if (!_registry.ContainsKey(key))
        {
            _registry[key] = new HashSet<StateObject>();
        }
        _registry[key].Add(entity);
    }

    private void OnTagRemoved(StateObject entity, Tag tag, Faction faction)
    {
        var key = (tag, faction);
        if (_registry.ContainsKey(key))
        {
            _registry[key].Remove(entity);
            
            // Clean up empty sets
            if (_registry[key].Count == 0)
            {
                _registry.Remove(key);
            }
        }
    }

    /// <summary>
    /// Gets all entities with the specified tag for a specific faction.
    /// </summary>
    public IEnumerable<StateObject> GetEntitiesWithTag(Tag tag, Faction faction)
    {
        var key = (tag, faction);
        var results = new HashSet<StateObject>();

        // Add entities with the specific faction tag
        if (_registry.ContainsKey(key))
        {
            results.UnionWith(_registry[key]);
        }

        // Also add entities with ALL factions tag
        var allKey = (tag, Faction.ALL);
        if (_registry.ContainsKey(allKey))
        {
            results.UnionWith(_registry[allKey]);
        }

        return results;
    }

    /// <summary>
    /// Gets all entities with the specified tag for ANY faction.
    /// </summary>
    public IEnumerable<StateObject> GetEntitiesWithTagAnyFaction(Tag tag)
    {
        var results = new HashSet<StateObject>();

        foreach (var kvp in _registry)
        {
            if (kvp.Key.Item1 == tag)
            {
                results.UnionWith(kvp.Value);
            }
        }

        return results;
    }

    /// <summary>
    /// Gets all entities that have ALL of the specified tags for a faction.
    /// </summary>
    public IEnumerable<StateObject> GetEntitiesWithAllTags(Faction faction, params Tag[] tags)
    {
        if (tags.Length == 0)
            return Enumerable.Empty<StateObject>();

        // Start with entities that have the first tag
        var result = GetEntitiesWithTag(tags[0], faction).ToHashSet();

        // Filter to only entities that have all other tags
        for (int i = 1; i < tags.Length; i++)
        {
            result.IntersectWith(GetEntitiesWithTag(tags[i], faction));
            if (result.Count == 0)
                break;
        }

        return result;
    }

    /// <summary>
    /// Gets all entities that have ANY of the specified tags for a faction.
    /// </summary>
    public IEnumerable<StateObject> GetEntitiesWithAnyTag(Faction faction, params Tag[] tags)
    {
        var result = new HashSet<StateObject>();
        
        foreach (var tag in tags)
        {
            result.UnionWith(GetEntitiesWithTag(tag, faction));
        }

        return result;
    }

    /// <summary>
    /// Gets all entities that do NOT have any of the specified tags for a faction.
    /// </summary>
    public IEnumerable<StateObject> GetEntitiesWithoutTags(Faction faction, params Tag[] tags)
    {
        var allEntities = new HashSet<StateObject>();
        foreach (var set in _registry.Values)
        {
            allEntities.UnionWith(set);
        }

        var entitiesWithTags = new HashSet<StateObject>();
        foreach (var tag in tags)
        {
            entitiesWithTags.UnionWith(GetEntitiesWithTag(tag, faction));
        }

        allEntities.ExceptWith(entitiesWithTags);
        return allEntities;
    }

    /// <summary>
    /// Gets the count of entities with the specified tag for a faction.
    /// </summary>
    public int GetCountWithTag(Tag tag, Faction faction)
    {
        return GetEntitiesWithTag(tag, faction).Count();
    }

    /// <summary>
    /// Gets all factions that have entities with the specified tag.
    /// </summary>
    public IEnumerable<Faction> GetFactionsWithTag(Tag tag)
    {
        var factions = new HashSet<Faction>();

        foreach (var key in _registry.Keys)
        {
            if (key.Item1 == tag)
            {
                factions.Add(key.Item2);
            }
        }

        return factions;
    }
}
