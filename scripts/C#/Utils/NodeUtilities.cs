using Godot;
using System;

public partial class NodeUtilities : SingletonNode<NodeUtilities>
{
    public Node GameNode => GetNode("/root/Game/");
    public Node PlayersNode => GameNode.GetNode("Players");
    public Node UnitsNode => GameNode.GetNode("Units");
    public Node2D WorldNode => GameNode.GetNode<Node2D>("World");    
    public Node CountriesNode => WorldNode != null ? WorldNode.GetNode("Countries") : null;
}
