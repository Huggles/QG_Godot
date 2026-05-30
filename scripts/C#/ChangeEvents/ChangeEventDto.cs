using System.Text.Json.Serialization;

/// <summary>
/// Polymorphic DTO hierarchy used to serialize ChangeEvents for multiplayer replication.
/// Only constructor parameters are included — never GodotObject references or derived state.
/// Register every new ChangeEvent subclass here with a unique string discriminator.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(DeployUnitChangeEventDto),       "DeployUnit")]
[JsonDerivedType(typeof(BattleCountryChangeEventDto),    "BattleCountry")]
[JsonDerivedType(typeof(RemoveUnitChangeEventDto),       "RemoveUnit")]
[JsonDerivedType(typeof(BattleUnitChangeEventDto),       "BattleUnit")]
[JsonDerivedType(typeof(PlayCardChangeEventDto),         "PlayCard")]
[JsonDerivedType(typeof(ActivateReactionChangeEventDto), "ActivateReaction")]
[JsonDerivedType(typeof(DiscardCardsChangeEventDto),     "DiscardCards")]
[JsonDerivedType(typeof(DrawCardsChangeEventDto),        "DrawCards")]
public abstract class ChangeEventDto
{
    public Faction TriggeringFaction    { get; set; }
    public int     SourceCardId         { get; set; } = -1;
    public bool    IsTrigger            { get; set; } = true;
    public bool    SuppressGameProgress { get; set; } = false;
}

public class DeployUnitChangeEventDto : ChangeEventDto
{
    public int        CountryId      { get; set; }
    public DeployType DeploymentType { get; set; }
}

public class BattleCountryChangeEventDto : ChangeEventDto
{
    public int CountryId { get; set; }
}

public class RemoveUnitChangeEventDto : ChangeEventDto
{
    public int               UnitId { get; set; }
    public UnitRemovalReason Reason { get; set; }
}

public class BattleUnitChangeEventDto : ChangeEventDto
{
    public int UnitId { get; set; }
}

public class PlayCardChangeEventDto : ChangeEventDto { }

public class ActivateReactionChangeEventDto : ChangeEventDto
{
    /// <summary>Id of the ChangeEvent being reacted to. Looked up via ChangeEvent.ForId() on the client.</summary>
    public int SourceChangeEventId { get; set; }
}

public class DiscardCardsChangeEventDto : ChangeEventDto
{
    public Faction TargetFaction { get; set; }
    public int     NumberOfCards { get; set; }
}

public class DrawCardsChangeEventDto : ChangeEventDto
{
    public Faction TargetFaction { get; set; }
    public int     NumberOfCards { get; set; }
}
