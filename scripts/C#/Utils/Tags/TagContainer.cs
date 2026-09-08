using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

/// <summary>
/// The (tag, faction) flags carried by one state object.
///
/// Storage is a flat <c>int[]</c> indexed by <see cref="Tag"/>, each entry a bitmask over
/// <see cref="Faction"/> — both enums are small, contiguous and 0-based, so a tag/faction pair is one
/// bit. This replaced a <c>Dictionary&lt;Tag, HashSet&lt;Faction&gt;&gt;</c> guarded by a lock, and the
/// rewrite was worth it for one reason: GameStateCalculator runs after every ChangeEvent and performs
/// tens of millions of tag operations per game across six parallel faction threads, all hitting the
/// SAME country/unit/card objects. Every read and write took that lock, so the threads serialised on
/// it and paid contention on top — measured at roughly DOUBLE the CPU work of running single-threaded.
///
/// Writes use <see cref="Interlocked"/> rather than a lock. Two threads setting different faction bits
/// of the same tag is the normal case here, and Or/And return the previous value, which is also
/// exactly what the change-detection below needs. Reads are plain int loads: naturally atomic, so a
/// reader sees a value from before or after a concurrent write, never a torn one.
///
/// Events fire OUTSIDE the atomic update and only on a real transition, matching the old behaviour.
/// </summary>
public partial class TagContainer : Node
{
    private static readonly int TagCount = Enum.GetValues<Tag>().Length;

    /// <summary>Bit for a faction. NONE is not storable — Add rejects it, as it always did.</summary>
    private static int Bit(Faction faction) => 1 << (int)faction;

    private static readonly int AllBit = Bit(Faction.ALL);

    private readonly int[] _bits = new int[TagCount];

    public event Action<Tag, Faction> TagAdded;
    public event Action<Tag, Faction> TagRemoved;

    /// <summary>
    /// Adds a tag for a specific faction.
    /// </summary>
    public bool Add(Tag tag, Faction faction)
    {
        if (faction == Faction.NONE)
            return false;

        int bit = Bit(faction);
        int previous = Interlocked.Or(ref _bits[(int)tag], bit);
        if ((previous & bit) != 0) return false;

        TagAdded?.Invoke(tag, faction);
        return true;
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
        int bit = Bit(faction);
        int previous = Interlocked.And(ref _bits[(int)tag], ~bit);
        if ((previous & bit) == 0) return false;

        TagRemoved?.Invoke(tag, faction);
        return true;
    }

    /// <summary>
    /// Removes a tag for all factions.
    /// </summary>
    public void RemoveForAll(Tag tag)
    {
        // Exchange once, then announce what was actually cleared — the old version snapshotted the
        // faction set and called Remove per faction, which is the same set of TagRemoved events.
        int previous = Interlocked.Exchange(ref _bits[(int)tag], 0);
        if (previous == 0 || TagRemoved == null) return;

        foreach (Faction faction in Enum.GetValues<Faction>())
            if ((previous & Bit(faction)) != 0)
                TagRemoved.Invoke(tag, faction);
    }

    /// <summary>
    /// Clears all tags for all factions.
    /// </summary>
    public void Clear()
    {
        for (int tag = 0; tag < TagCount; tag++)
            RemoveForAll((Tag)tag);
    }

    /// <summary>
    /// Checks if a tag is present for a specific faction.
    /// Also returns true if the tag has Faction.ALL.
    /// </summary>
    public bool Has(Tag tag, Faction faction) => (_bits[(int)tag] & (Bit(faction) | AllBit)) != 0;

    /// <summary>
    /// Checks if a tag is present for ANY faction.
    /// </summary>
    public bool HasForAny(Tag tag) => _bits[(int)tag] != 0;

    /// <summary>
    /// Gets all factions that have a specific tag.
    /// </summary>
    public IEnumerable<Faction> GetFactionsWithTag(Tag tag)
    {
        int bits = _bits[(int)tag];
        if (bits == 0) return Enumerable.Empty<Faction>();

        List<Faction> factions = new();
        foreach (Faction faction in Enum.GetValues<Faction>())
            if ((bits & Bit(faction)) != 0)
                factions.Add(faction);
        return factions;
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
    public IEnumerable<Tag> GetAllTags()
    {
        List<Tag> tags = new();
        for (int tag = 0; tag < TagCount; tag++)
            if (_bits[tag] != 0) tags.Add((Tag)tag);
        return tags;
    }

    /// <summary>
    /// Returns all tags that apply to a specific faction.
    /// </summary>
    public IEnumerable<Tag> GetTagsForFaction(Faction faction)
    {
        int mask = Bit(faction) | AllBit;
        List<Tag> tags = new();
        for (int tag = 0; tag < TagCount; tag++)
            if ((_bits[tag] & mask) != 0) tags.Add((Tag)tag);
        return tags;
    }

    /// <summary>
    /// Returns the total number of unique tags.
    /// </summary>
    public int Count
    {
        get
        {
            int count = 0;
            for (int tag = 0; tag < TagCount; tag++)
                if (_bits[tag] != 0) count++;
            return count;
        }
    }

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
