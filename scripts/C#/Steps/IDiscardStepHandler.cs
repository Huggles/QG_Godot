using Godot;
using System;
using System.Threading.Tasks;

public interface IDiscardStepHandler
{
    void Start(Faction faction);
    event Action DiscardStepFinished;
}
