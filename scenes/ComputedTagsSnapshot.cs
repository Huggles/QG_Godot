using System.Collections.Generic;

/// <summary>
/// Wire-format snapshot of all server-computed tags that need to be replicated to clients.
/// CardStep tags (IsExecutable) are intentionally excluded — they are server-internal
/// execution state that clients never need to observe.
/// </summary>
public class ComputedTagsSnapshot
{
    public List<TagEntry> Entries { get; set; } = new();
}

/// <summary>
/// A single tag assignment on a specific state object.
/// ObjectType is "Country", "Unit", "Card", or "Straight".
/// </summary>
public class TagEntry
{
    public string ObjectType { get; set; }
    public int    Id         { get; set; }
    public Tag    Tag        { get; set; }
    public Faction Faction   { get; set; }
}
