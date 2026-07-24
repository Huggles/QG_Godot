using System.Collections.Generic;
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
[JsonDerivedType(typeof(ForceDiscardCardsChangeEventDto),     "DiscardCards")]
[JsonDerivedType(typeof(DrawCardsChangeEventDto),        "DrawCards")]
[JsonDerivedType(typeof(ScorePointsChangeEventDto),      "ScorePoints")]
[JsonDerivedType(typeof(SetStartingScoreChangeEventDto), "SetStartingScore")]
[JsonDerivedType(typeof(DiscardHandCardsChangeEventDto), "VoluntaryDiscardCards")]
[JsonDerivedType(typeof(ForceDiscardHandCardsChangeEventDto), "ForceDiscardHandCards")]
[JsonDerivedType(typeof(DrawCardByNameChangeEventDto),   "DrawCardByName")]
[JsonDerivedType(typeof(ChangeStepChangeEventDto),       "ChangeStep")]
[JsonDerivedType(typeof(ChangeRoundChangeEventDto),      "ChangeRound")]
[JsonDerivedType(typeof(RecycleCardChangeEventDto),      "RecycleCard")]
[JsonDerivedType(typeof(SpendPlayActionChangeEventDto),  "SpendPlayAction")]
[JsonDerivedType(typeof(ReorderDeckChangeEventDto),      "ReorderDeck")]
[JsonDerivedType(typeof(GrantSupplyChangeEventDto),       "GrantSupply")]
[JsonDerivedType(typeof(RecalculateTagsChangeEventDto),   "RecalculateTags")]
public abstract class ChangeEventDto
{
    public int Id { get; set; }
    public string HashAfterApplication { get; set; }
    public Faction TriggeringFaction    { get; set; }
    public int     SourceCardId         { get; set; } = -1;
    public bool    IsTrigger            { get; set; } = true;
    public bool    SuppressGameProgress { get; set; } = false;
    public bool    PlayAnimations        { get; set; } = true;
    public bool    BlockAnimationQueue   { get; set; } = true;
    public Faction TargetFaction { get; set; }

    public static T Build<T>(ChangeEvent handler, int Id) where T : ChangeEventDto, new()
    {
        T dto = new T();
        dto.Id = Id;
        dto.HashAfterApplication = handler.HashAfterApplication;
        dto.TriggeringFaction = handler.TriggeringFaction;        
        dto.TargetFaction = handler.TargetFaction;
        dto.SourceCardId = handler.SourceCardId;
        dto.IsTrigger = handler.IsTrigger;
        dto.SuppressGameProgress = handler.SuppressGameProgress;
        dto.PlayAnimations = handler.PlayAnimations;
        dto.BlockAnimationQueue = handler.BlockAnimationQueue;
        return dto;
    }
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

public class ForceDiscardCardsChangeEventDto : ChangeEventDto
{
    public int     NumberOfCards { get; set; }
}

public class DrawCardsChangeEventDto : ChangeEventDto
{
    
    public int     NumberOfCards { get; set; }
    public bool    ShowDrawnCards { get; set; }
}

public class ScorePointsChangeEventDto : ChangeEventDto
{
    public VPTurnSummary VPTurnSummary { get; set; }
}

public class SetStartingScoreChangeEventDto : ChangeEventDto
{
    public int Score { get; set; }
}

public class DiscardHandCardsChangeEventDto : ChangeEventDto
{
    public List<int> CardIds { get; set; }
}

public class ForceDiscardHandCardsChangeEventDto : ChangeEventDto
{
    public int NumberOfCards { get; set; }
}

public class DrawCardByNameChangeEventDto : ChangeEventDto
{
    public string CardName { get; set; }
}

public class ChangeStepChangeEventDto : ChangeEventDto
{
    public TurnStep NewStep { get; set; }
}

public class ChangeRoundChangeEventDto : ChangeEventDto
{
    public int NewTurn { get; set; }
}

public class RecycleCardChangeEventDto : ChangeEventDto
{
    public int CardId { get; set; }
    public RecycleDestination Destination { get; set; }
}

/// <summary>No extra fields needed — TriggeringFaction is sufficient.</summary>
public class SpendPlayActionChangeEventDto : ChangeEventDto { }

public class ReorderDeckChangeEventDto : ChangeEventDto
{
    public List<int> ReorderedCardIds { get; set; }
}

public class GrantSupplyChangeEventDto : ChangeEventDto
{
    public List<int> UnitIds { get; set; }
}

public class RecalculateTagsChangeEventDto : ChangeEventDto
{
    public ComputedTagsSnapshot Snapshot { get; set; }
}
