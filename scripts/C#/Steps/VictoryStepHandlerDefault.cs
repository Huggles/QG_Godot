using Godot;
using System;

public partial class VictoryStepHandlerDefault : IVictoryStepHandler
{
    public void ProcessVictoryStep(Faction faction)
    {
        int round = GameSession.Instance.GameFlow.Round;
    }

}
