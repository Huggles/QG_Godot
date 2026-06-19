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
    IsPlayed,
    IsDiscarded,
    

    //Unit Tags
    InSupply,

    //UI Tags
    Clickable,

}
