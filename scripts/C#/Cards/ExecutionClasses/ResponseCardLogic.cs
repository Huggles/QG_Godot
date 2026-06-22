using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public abstract partial class ResponseCardLogic : CardLogic
{
    public override List<CardStep> OnActivate() => new();
}
