using Godot;
using System;
using System.Threading;
using System.Threading.Tasks;

public partial class SessionLoader : Node
{

    [Export]
    private Node _dataNode;

    [Export]
    private MultiplayerSpawner _multiplayerSpawner;


    public static readonly PackedScene multiplayerSessionScenePacked = GD.Load<PackedScene>("res://scenes/network/MultiplayerSession.tscn");

    public override void _Ready()
    {        
        SetMultiplayerAuthority(1);
        DebugUtilities.PrintPeer($"SessionLoader _Ready called. Multiplayer Authority set to 1 (host).", DebugVerbosity.INFO);
        if (Multiplayer.IsServer())
        {     
            StartGameSession();
        }
        DebugUtilities.PrintPeer($"SessionLoader ready", DebugVerbosity.INFO);        
    }

    public async void StartGameSession()
    {
        await Task.Delay(2000);
        if (Multiplayer.IsServer())
        {
            Rpc(nameof(Debug));

            



            MultiplayerSession multiplayerSessionInstance = multiplayerSessionScenePacked.Instantiate<MultiplayerSession>();            
            multiplayerSessionInstance.SetMultiplayerAuthority(1); // host is authority  
            _dataNode.AddChild(multiplayerSessionInstance, true);   
            DebugUtilities.PrintPeer($"MultiplayerSession created and added to scene tree", DebugVerbosity.INFO);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void Debug() { 
        DebugUtilities.PrintPeer($"Debug RPC called on SessionLoader", DebugVerbosity.INFO);
    }
}
