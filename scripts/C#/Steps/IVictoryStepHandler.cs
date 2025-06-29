using Godot;
using System;
using System.Threading.Tasks;

public interface IVictoryStepHandler
{
    Task ProcessVictoryStep(Faction faction);
    Task ScorePoints(VPEntry vPEntry);
}
