using Godot;
using System;

public static partial class Game
{
    public static GameState GameState => GameSession.Instance.GameState;
    public static GameFlow GameFlow => GameSession.Instance.GameFlow;
    public static IGameMode GameMode => GameSession.Instance.GameMode;
}
