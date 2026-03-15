using Godot;
using System;
using System.Threading.Tasks;

public interface IDrawStepHandler
{
    void Start(Faction faction);
    event Action DrawStepFinished;
}
