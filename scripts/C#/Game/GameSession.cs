using Godot;
using System;

public partial class GameSession : Node
{
    private static GameSession instance;
    public static GameSession Instance {
        get {
            if (instance == null) {
                throw new System.ArgumentNullException("GameSession Not Initialized");
            }
            return instance;
        }
    }



    public IGameMode GameMode;
    [Export] public GameState GameState;
    [Export] public GameFlow GameFlow;
     
    
    

    public GameSession(){
        instance = this;

        
    }

    public void StartSession(){
        DebugUtilities.PrintPeer("Start Session");
        GameState = new GameState();
        GameMode = new GameModeDefault();
        GameMode.Init();

        GameFlow = new GameFlow();

    }
}
