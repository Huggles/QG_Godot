using Godot;
using System;

public partial class NodeUtilities : SingletonNode<NodeUtilities>
{
    public Node GameNode => GetNode("/root/Game/");
    public Node PlayersNode => GameNode.GetNode("Players");
    public Control UnitsNode => GameNode.GetNode<Control>("%Units");
    public Control WorldNode => GameNode.GetNode<Control>("%World");
    public Control CountriesNode => WorldNode != null ? WorldNode.GetNode<Control>("%Countries") : null;
    /// <summary>
    /// The picture-in-picture camera, or null before the HUD exists (menu, headless). Read off
    /// <see cref="TriggerContextDisplay"/> rather than looked up here: the viewport lives inside that
    /// panel's scene, and "%FocusCamera" only resolves from within the scene that owns it.
    /// </summary>
    public Camera2D FocusCamera => TriggerContextDisplay.Current?.FocusCamera;

    /// <summary>
    /// The board backdrop. Its rect is what the camera is bound to, so it is the single place that
    /// decides how far the player may pan and how far out they may zoom — resize the BackgroundPanel in
    /// Game.tscn and the camera follows, no constants to keep in sync.
    /// Null before the Game scene exists (menu, lobby) and headless.
    /// </summary>
    public Control BoardNode => GetNodeOrNull<Control>("/root/Game/BackgroundPanel");

    /// <summary>
    /// <see cref="BoardNode"/>'s rect in world space, or null when the board is not in the tree.
    /// Built from the global transform rather than <c>GetGlobalRect()</c>: the board may sit under a
    /// scaled parent, and a Control's Size is in its own local space — only the transform
    /// carries that scale.
    /// </summary>
    public Rect2? BoardBounds
    {
        get
        {
            Control board = BoardNode;
            if (board == null) return null;

            Transform2D transform = board.GetGlobalTransform();
            Vector2 topLeft = transform * Vector2.Zero;
            Vector2 bottomRight = transform * board.Size;
            return new Rect2(topLeft, bottomRight - topLeft).Abs();
        }
    }
}
