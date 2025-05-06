using Godot;
using System;

public partial class SingletonNode<T> : Node where T : SingletonNode<T>
{
    public static T Instance;
    
    public override void _Ready()
    {
        Instance = (T)this;
    }

}
