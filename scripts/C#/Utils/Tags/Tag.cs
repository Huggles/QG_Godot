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

    /// <summary>
    /// On a COUNTRY or a UNIT: the player is hovering a card in an open card prompt and this is one of
    /// the things the card could affect. Drawn with the same visual as an offered target, deliberately
    /// — the player is being shown "this is what that card reaches" — but WITHOUT the click, because
    /// the game is still waiting for a card, not for a country or a unit.
    ///
    /// A country glows; a unit puts up its own target marker and leaves its country dark, so a card
    /// that reaches one army in Egypt says exactly that rather than lighting the whole country.
    ///
    /// Raised and cleared only by <see cref="CardTargetPreviewDisplay"/> on the hovering peer. Local
    /// like <see cref="Clickable"/> and <see cref="RebuildTarget"/>: it is pure presentation and is
    /// not in GameStateCalculator.ReplicatedTags. New values go at the END of this enum — TagEntry
    /// serializes a Tag numerically, so inserting one would reinterpret every replicated tag.
    /// </summary>
    PreviewTarget,

    /// <summary>
    /// On a UNIT: the focus viewport beside a reaction window's trigger card is pointed at the country
    /// this unit stands in, and this is the unit the triggering event reached. Drawn with the same
    /// marker an offered target gets, but ONLY inside that viewport — see
    /// <see cref="WorldMirrorViewport.FocusOnlyLayer"/> — and never clickable.
    ///
    /// Countries are deliberately never given this tag: the focus view frames the country and
    /// indicates the unit, so glowing the country as well would say the same thing twice and drown the
    /// one mark that identifies which unit is meant.
    ///
    /// Its own tag rather than a second use of <see cref="PreviewTarget"/> because the two want
    /// different visibility layers on the same node — a card hover preview belongs on the real board,
    /// this belongs only in the focus view — and because TagContainer does not reference count, so two
    /// raisers of one tag cannot see each other.
    ///
    /// Raised and cleared only by <see cref="FocusTargetDisplay"/> on the peer being asked. Local like
    /// <see cref="Clickable"/> and <see cref="PreviewTarget"/>: pure presentation, and not in
    /// GameStateCalculator.ReplicatedTags. New values go at the END of this enum — TagEntry serializes
    /// a Tag numerically, so inserting one would reinterpret every replicated tag.
    /// </summary>
    FocusTarget,
}
