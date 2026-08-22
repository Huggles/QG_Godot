using Godot;
using System;

public enum Tag
{   
    //Country Tags
    Attackable,    
    EnemyControlled,
    AlliedControlled,
    Empty,
    Buildable,
    Recruitable,
    LandCountry,
    SeaCountry,

    //Straight Tags
    AxisControlled,
    AlliesControlled,

    //Card Tags    
    IsPlayable,
    IsActivatable,
    IsAfterReaction,
    IsBlockReaction,
    IsPlayed,
    IsDiscarded,
    IsBlocked,

    //CardStep Tags
    IsExecutable,
    

    //Unit Tags
    InSupply,
    Immune,
    SuppliedForTurn,
    OutOfSupply,

    //UI Tags
    Clickable,

    /// <summary>
    /// On a UNIT: clicking it answers the open country selection with the country it stands on. Raised
    /// for any offered country the asked faction already occupies, which in practice always means a
    /// deploy target — "build that army again" in place, since SelectCountryRequestHandler is the
    /// deploy-target picker. Drawn smaller and fainter than an ordinary target because it is the rare
    /// option sitting beside the ordinary ones.
    ///
    /// Local like <see cref="Clickable"/>: it is raised by SelectCountryHandler on the answering peer
    /// and is not in GameStateCalculator.ReplicatedTags. New values go at the END of this enum —
    /// TagEntry serializes a Tag numerically, so inserting one would reinterpret every replicated tag.
    /// </summary>
    RebuildTarget,

    /// <summary>
    /// On a CARD: it can be played, but on the current board every effect it offers is hollow — the
    /// canonical case being a build card whose only remaining targets are countries the faction
    /// already occupies, where GameAPI.DeployUnitToCountry rebuilds in place and the board does not
    /// change. Drawn with a caution scrim rather than hidden or blocked: the play is legal and
    /// sometimes wanted (it is still a reactable DeployUnitChangeEvent), it just is not what a player
    /// reaching for the card usually means.
    ///
    /// Raised by GameStateCalculator.CalculateAttentionCardsForFaction from the advisory conditions on
    /// the card's executable steps (<see cref="CardStep.MeetAllAdvisoryConditions"/>), and replicated —
    /// the client renders the hand, so it must know. New values go at the END of this enum, see
    /// <see cref="RebuildTarget"/>.
    /// </summary>
    NeedsAttention,
}
