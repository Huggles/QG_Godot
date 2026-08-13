using System.Collections.Generic;

/// <summary>
/// Wire format for a <see cref="ChangeEvent"/> — the state-mutating branch of the game-message stream.
/// Only constructor parameters are included, never GodotObject references or derived state.
///
/// The polymorphic registry lives one level up, on <see cref="GameMessageDto"/>: there is a single
/// [JsonPolymorphic] root for all three branches so that System.Text.Json has exactly one
/// configuration to resolve. Register a new ChangeEvent's discriminator there, not here.
///
/// The fields below are the ones only a mutation needs — a hash to verify against, and the reaction-
/// chain flags. Everything the channel itself needs (Id, factions, animation knobs) is on the base.
/// </summary>
public abstract class ChangeEventDto : GameMessageDto
{
    public string  HashAfterApplication { get; set; }
    public int     SourceCardId         { get; set; } = -1;
    public bool    IsTrigger            { get; set; } = true;
    public bool    SuppressGameProgress { get; set; } = false;

    public static T Build<T>(ChangeEvent handler, int Id) where T : ChangeEventDto, new()
    {
        T dto = new T();
        Fill(dto, handler, Id);
        dto.HashAfterApplication = handler.HashAfterApplication;
        dto.SourceCardId = handler.SourceCardId;
        dto.IsTrigger = handler.IsTrigger;
        dto.SuppressGameProgress = handler.SuppressGameProgress;
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

    /// <summary>
    /// Whether this activation is the card's first step. Carried on the wire rather than recomputed:
    /// only the server runs CardStep.Execute, so a client cannot tell a resumed step from a first one.
    /// </summary>
    public bool IsFirstStep { get; set; } = true;
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

    /// <summary>
    /// The cards the target actually picked. On the wire because the client cannot work them out:
    /// the choice is a player's, and re-running the input request on a replaying peer would send an
    /// Authority-mode Rpc from a client. See ForceDiscardHandCardsChangeEvent.SelectionResolved.
    /// </summary>
    public List<int> DiscardedCardIds { get; set; }
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

    /// <summary>
    /// Host-computed deck order for a ShuffleIntoDeck recycle; null otherwise. Restored by
    /// RecycleCardChangeEvent.ApplyDtoFields, not by the constructor.
    /// </summary>
    public List<int> ShuffledOrder { get; set; }
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

public class RegisterBulletinCardChangeEventDto : ChangeEventDto
{
    public int    CardId           { get; set; }
    public string MutatorClassName { get; set; }
    public int    FromRound        { get; set; }
    public int    ToRound          { get; set; }
}
