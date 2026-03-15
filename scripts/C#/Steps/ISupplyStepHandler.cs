using Godot;
using System;
using System.Threading.Tasks;

public interface ISupplyStepHandler
{
    void Start(Faction faction);
    event Action SupplyStepFinished;
}
