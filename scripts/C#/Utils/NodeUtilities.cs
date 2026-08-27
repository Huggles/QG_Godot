using Godot;
using System;

public partial class NodeUtilities : SingletonNode<NodeUtilities>
{
    public Node GameNode => GetNode("/root/Game/");
    public Node PlayersNode => GameNode.GetNode("Players");
    public Node UnitsNode => GameNode.GetNode("Units");
    public Node2D WorldNode => GameNode.GetNode<Node2D>("World");    
    public Node CountriesNode => WorldNode != null ? WorldNode.GetNode("Countries") : null;
    /// <summary>
    /// The picture-in-picture camera in the local player's scene, or null before a game exists
    /// (menu, headless). Resolved from <see cref="PlayerScene.Current"/> rather than from
    /// <see cref="GameNode"/>: "%FocusCamera" is unique within Player.tscn, and % names only
    /// resolve inside the scene that owns them — Game.tscn cannot see it.
    /// </summary>
    public Camera2D FocusCamera => PlayerScene.Current?.GetNodeOrNull<Camera2D>("%FocusCamera");

    /// <summary>
    /// The board backdrop. Its rect is what the camera is bound to, so it is the single place that
    /// decides how far the player may pan and how far out they may zoom — resize the NinePatchRect in
    /// WorldScene.tscn and the camera follows, no constants to keep in sync.
    /// Null before the Game scene exists (menu, lobby) and headless.
    /// </summary>
    public NinePatchRect BoardNode => GetNodeOrNull<NinePatchRect>("/root/Game/World/Background/NinePatchRect");

    /// <summary>
    /// <see cref="BoardNode"/>'s rect in world space, or null when the board is not in the tree.
    /// Built from the global transform rather than <c>GetGlobalRect()</c>: the board sits under a
    /// scaled Background node, and a Control's Size is in its own local space — only the transform
    /// carries that scale.
    /// </summary>
    public Rect2? BoardBounds
    {
        get
        {
            NinePatchRect board = BoardNode;
            if (board == null) return null;

            Transform2D transform = board.GetGlobalTransform();
            Vector2 topLeft = transform * Vector2.Zero;
            Vector2 bottomRight = transform * board.Size;
            return new Rect2(topLeft, bottomRight - topLeft).Abs();
        }
    }
}
