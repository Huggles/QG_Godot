using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>A Bolster card: a reaction used straight from hand, then discarded. How it is offered and used arrives with M4.</summary>
public abstract partial class BolsterCardLogic : CardLogic
{
    public override List<CardStep> OnActivate() => new();
}
