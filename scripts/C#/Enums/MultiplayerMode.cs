using Godot;
using System;

/// <summary>
/// Defines different multiplayer game configurations
/// </summary>
public enum MultiplayerMode
{
    /// <summary>
    /// Single player controls all 6 factions
    /// </summary>
    SINGLE_PLAYER,
    
    /// <summary>
    /// 2 players: Player 1 controls AXIS (Germany, Japan, Italy), Player 2 controls ALLIES (UK, Soviet, US)
    /// </summary>
    TWO_PLAYER_TEAMS,
    
    /// <summary>
    /// 6 players: One player per faction
    /// </summary>
    SIX_PLAYER,
    
    /// <summary>
    /// 3 players: One AXIS player, two ALLIES players
    /// Future implementation
    /// </summary>
    THREE_PLAYER_MIXED
}
