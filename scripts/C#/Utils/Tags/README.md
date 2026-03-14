# Faction-Aware Tag System - Quick Reference

This tag system allows you to assign flags/tags to game objects that are faction-specific. Tags like "Attackable" can apply to some factions but not others.

## Shorthand Query Syntax (Recommended)

Use extension methods for concise, readable queries:

```csharp
// Get playable cards
var playableCards = hand.WithTag(Tag.Playable, currentFaction).ToList();

// Get buildable empty territories
var buildable = territories.WithAllTags(currentFaction, Tag.Buildable, Tag.Empty).ToList();

// Count attackable territories
int count = territories.CountWithTag(Tag.Attackable, currentFaction);

// Check if any cards are playable
if (hand.AnyWithTag(Tag.Playable, currentFaction)) { }

// Find first playable card
var card = hand.FirstOrDefaultWithTag(Tag.Playable, currentFaction);

// Batch add tag to multiple items
cards.AddTag(Tag.Playable, currentFaction);

// Chain filters
var targets = territories
    .WithTag(Tag.Attackable, currentFaction)
    .WithoutTag(Tag.AlliedControlled, currentFaction)
    .ToList();
```

## Basic Concepts

- **Tags**: Enum values (e.g., `Tag.Attackable`, `Tag.Buildable`)
- **Factions**: `GERMANY`, `ITALY`, `JAPAN`, `UNITED_KINGDOM`, `UNITED_STATES`, `SOVIET`
- **Special Factions**: 
  - `Faction.ALL` - Tag applies to all factions
  - `Faction.NONE` - No faction (used as sentinel value)

## Common Usage Patterns

### Adding Tags

```csharp
// Add tag for specific faction
territory.AddTag(Tag.Attackable, Faction.GERMANY);

// Add tag for all factions
territory.AddTagForAll(Tag.Clickable);

// Add multiple tags for a faction
var tags = territory.GetTags();
tags.AddRange(Faction.SOVIET, Tag.Buildable, Tag.Recruitable);
```

### Checking Tags

```csharp
// Check if tag applies to specific faction
if (territory.HasTag(Tag.Attackable, currentFaction))
{
    // Can attack
}

// Check if tag exists for ANY faction
if (territory.HasTagAnyFaction(Tag.Attackable))
{
    // Some faction can attack
}

// Check multiple tags for a faction
if (territory.HasAllTags(currentFaction, Tag.Buildable, Tag.Empty))
{
    // Can build here
}

// Get all factions that have a tag
var factions = territory.GetFactionsWithTag(Tag.Attackable);
// Returns: GERMANY, ITALY, etc.
```

### Removing Tags

```csharp
// Remove for specific faction
territory.RemoveTag(Tag.Attackable, Faction.GERMANY);

// Remove for all factions
territory.RemoveTagForAll(Tag.Attackable);
```

### Finding Tagged Nodes

```csharp
// Find nodes with tag for current player's faction
var attackable = GetTree().Root.FindChildrenWithTag(
    Tag.Attackable, 
    currentPlayerFaction
);

// Find nodes with tag for ANY faction
var anyAttackable = GetTree().Root.FindChildrenWithTagAnyFaction(Tag.Attackable);

// Find nodes with multiple tags for a faction
var buildableLocations = GetTree().Root.FindChildrenWithAllTags(
    currentPlayerFaction,
    Tag.Buildable,
    Tag.Empty
);

// Find first match
var territory = GetTree().Root.FindFirstChildWithTag(
    Tag.Attackable, 
    Faction.GERMANY
);
```

### Using TagRegistry (for StateObjects)

```csharp
// Get entities attackable by Germany
var targets = tagRegistry.GetEntitiesWithTag(Tag.Attackable, Faction.GERMANY);

// Get entities with multiple tags for a faction
var locations = tagRegistry.GetEntitiesWithAllTags(
    Faction.JAPAN,
    Tag.Recruitable,
    Tag.Empty
);

// Count entities
int count = tagRegistry.GetCountWithTag(Tag.AlliedControlled, Faction.ITALY);
```

### Responding to Tag Changes

```csharp
var tags = territory.GetTags();

tags.TagAdded += (tag, faction) => 
{
    GD.Print($"Tag {tag} added for {faction}");
};

tags.TagRemoved += (tag, faction) => 
{
    GD.Print($"Tag {tag} removed for {faction}");
};
```

## Typical Game Flow Examples

### When a Territory is Captured

```csharp
void CaptureTerritory(Node territory, Faction capturingFaction, Faction previousOwner)
{
    // Remove old owner's tags
    territory.RemoveTag(Tag.AlliedControlled, previousOwner);
    territory.RemoveTag(Tag.Buildable, previousOwner);
    territory.RemoveTag(Tag.Recruitable, previousOwner);
    
    // Add new owner's tags
    territory.AddTag(Tag.AlliedControlled, capturingFaction);
    territory.AddTag(Tag.Buildable, capturingFaction);
    territory.AddTag(Tag.Recruitable, capturingFaction);
    
    // Update who can attack it (all enemy factions)
    UpdateAttackableTags(territory, capturingFaction);
}
```

### Setting Up Attackability

```csharp
void UpdateAttackableTags(Node territory, Faction controllingFaction)
{
    // Remove attackable for all
    territory.RemoveTagForAll(Tag.Attackable);
    
    // Add attackable for enemy factions only
    var allFactions = new[] 
    { 
        Faction.GERMANY, Faction.ITALY, Faction.JAPAN,
        Faction.UNITED_KINGDOM, Faction.UNITED_STATES, Faction.SOVIET
    };
    
    foreach (var faction in allFactions)
    {
        if (IsEnemy(faction, controllingFaction))
        {
            territory.AddTag(Tag.Attackable, faction);
        }
    }
}
```

### Player Turn Logic

```csharp
void OnTerritoryClicked(Node territory, Faction currentPlayerFaction)
{
    if (territory.HasTag(Tag.Attackable, currentPlayerFaction))
    {
        InitiateAttack(territory, currentPlayerFaction);
    }
    else if (territory.HasAllTags(currentPlayerFaction, Tag.Buildable, Tag.AlliedControlled))
    {
        ShowBuildMenu(territory, currentPlayerFaction);
    }
    else if (territory.HasTag(Tag.Recruitable, currentPlayerFaction))
    {
        ShowRecruitMenu(territory, currentPlayerFaction);
    }
    else
    {
        GD.Print("No valid actions for this faction");
    }
}
```

### Finding Valid Actions

```csharp
void UpdateUIForCurrentPlayer(Faction currentFaction)
{
    // Highlight all attackable territories
    var attackable = GetTree().Root.FindChildrenWithTag(
        Tag.Attackable, 
        currentFaction
    );
    
    foreach (var territory in attackable)
    {
        HighlightTerritory(territory, Color.Color8(255, 0, 0)); // Red
    }
    
    // Highlight buildable locations
    var buildable = GetTree().Root.FindChildrenWithAllTags(
        currentFaction,
        Tag.Buildable,
        Tag.Empty
    );
    
    foreach (var territory in buildable)
    {
        HighlightTerritory(territory, Color.Color8(0, 255, 0)); // Green
    }
}
```

## Performance Tips

1. **Use TagRegistry for StateObjects**: More efficient than scene tree queries
2. **Check specific factions**: `HasTag(tag, faction)` is faster than `HasTagAnyFaction(tag)`
3. **Batch updates**: Use `AddRange()` instead of multiple `Add()` calls when possible
4. **Clean up unused tags**: Tags are automatically removed when no factions have them

## Common Patterns

### Shorthand Extension Methods (IEnumerable<ITaggable>)

**Filtering:**
- `.WithTag(tag, faction)` - Items with tag for faction
- `.WithTagAnyFaction(tag)` - Items with tag for any faction
- `.WithAllTags(faction, tags...)` - Items with all tags for faction
- `.WithAnyTag(faction, tags...)` - Items with any of the tags for faction
- `.WithoutTag(tag, faction)` - Items without tag for faction
- `.WithoutAnyTag(faction, tags...)` - Items with none of the tags

**Counting & Checking:**
- `.CountWithTag(tag, faction)` - Count items with tag
- `.AnyWithTag(tag, faction)` - Check if any item has tag
- `.AllWithTag(tag, faction)` - Check if all items have tag
- `.FirstOrDefaultWithTag(tag, faction)` - Get first item with tag

**Batch Operations:**
- `.AddTag(tag, faction)` - Add tag to all items for faction
- `.AddTagForAll(tag)` - Add tag to all items for all factions
- `.RemoveTag(tag, faction)` - Remove tag from all items for faction
- `.RemoveTagForAll(tag)` - Remove tag from all items for all factions

### Faction-Agnostic Tags (UI elements)
```csharp
// UI elements that work for everyone
button.AddTagForAll(Tag.Clickable);
card.AddTagForAll(Tag.Playable);
```

### Faction-Specific Tags (Game state)
```csharp
// Territory control and permissions
territory.AddTag(Tag.AlliedControlled, Faction.GERMANY);
territory.AddTag(Tag.Attackable, Faction.SOVIET);
territory.AddTag(Tag.Buildable, Faction.GERMANY);
```

### Querying Complex Conditions
```csharp
// Find territories I control that are under threat
var myTerritories = tagRegistry.GetEntitiesWithTag(Tag.AlliedControlled, myFaction);
var threatened = myTerritories.Where(t => 
    t.Tags.HasTagAnyFaction(Tag.Attackable)
);
```
