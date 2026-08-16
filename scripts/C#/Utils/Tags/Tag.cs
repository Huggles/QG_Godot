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

}
