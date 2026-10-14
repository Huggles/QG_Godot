using System.Collections.Generic;

/// <summary>
/// M0a spike only: proves a CardLogic subclass compiled into an expansion assembly is a working Godot
/// script once ExpansionLoader has loaded it. Delete with the spike.
/// </summary>
public partial class SpikeCard : EventCardLogic
{
    public override List<CardStep> OnActivate() => new();

    /// <summary>
    /// Called through Godot (GodotObject.Call) by ExpansionSelfTest, which only works if Godot registered
    /// this class as a script.
    /// </summary>
    public string SpikeEcho(string text) => text;
}
