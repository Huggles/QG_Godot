using Godot;
using System;

public partial class SingletonNode<T> : Node where T : SingletonNode<T>
{
    public static T Instance;

    public override void _Ready()
    {
        Instance = (T)this;
    }

    /// <summary>
    /// Drop the pointer on the way out, so callers get the null that `Instance?.` is written against.
    /// Without this the static keeps a freed Godot wrapper, and every `Instance?.Foo()` throws
    /// ObjectDisposedException instead of skipping — which is exactly what would happen to GameFlow
    /// once SceneFlow frees the MultiplayerSession subtree it lives in.
    ///
    /// Guarded on identity: a singleton that is replaced before the old one leaves the tree must not
    /// have the outgoing node erase the incoming one's registration.
    ///
    /// Every other SingletonNode in the project is an autoload, so this only fires at shutdown for them.
    /// </summary>
    public override void _ExitTree()
    {
        base._ExitTree();
        if (Instance == this) Instance = null;
    }
}
