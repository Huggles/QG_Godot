using Godot;
using System;

public partial class NodeUtilities : SingletonNode<NodeUtilities>
{
    public Node GameNode;
    public Node PlayersNode;
    public Node UnitsNode;
    public Node2D WorldNode;
    public CanvasLayer UserInterface;
    
    public Node CountriesNode => WorldNode != null ? WorldNode.GetNode("Countries") : null;

    public override void _Ready()
    {
        base._Ready();
        GD.Print("NodeUtilities Ready");
        GameNode = GetTree().Root.GetNode("Game");
        PlayersNode = GameNode.GetNode("Players");
        UnitsNode = GameNode.GetNode("Units");
        WorldNode = GameNode.GetNode<Node2D>("World");
        UserInterface = GameNode.GetNode<CanvasLayer>("UserInterface");
    }
}
