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
        InitializeGameNodes();
    }

    public void InitializeGameNodes()
    {
        // Try to find the Game node - it may not exist if we're in the menu/lobby
        GameNode = GetTree().Root.GetNodeOrNull("Game");
        
        if (GameNode != null)
        {
            PlayersNode = GameNode.GetNode("Players");
            UnitsNode = GameNode.GetNode("Units");
            WorldNode = GameNode.GetNode<Node2D>("World");
            UserInterface = GameNode.GetNode<CanvasLayer>("UserInterface");
            GD.Print("NodeUtilities: Game nodes initialized");
        }
        else
        {
            GD.Print("NodeUtilities: Game node not found (probably in menu/lobby)");
        }
    }
}
