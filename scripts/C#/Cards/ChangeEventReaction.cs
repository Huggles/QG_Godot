using Godot;
using System;

public partial class ChangeEventReaction : Node
{
    public ChangeEvent ChangeEvent;
    public ChangeEventReaction(ChangeEvent changeEvent){
        ChangeEvent = changeEvent;
    }
}
