using Godot;
using System;

using Games.Indiegesindel;


public partial class SteamworksApi : Node
{

	long lobbyId = -1;
	private Steam _instance { get; set; }
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		 _instance = Steam.GetSingleton();
		 // Fetch IsSteamRunning
		if (_instance != null)
		{	
			bool isRunning = _instance.IsSteamRunning();
			DebugUtilities.PrintPeer("initialized SteamProcess, isRunning: " + isRunning);
			
			var name = _instance.GetPersonaName();
			var state = _instance.GetPersonaState();
			var appId = _instance.GetAppId();

			_instance.LobbyCreatedSignal += (id, result) => {
				lobbyId = id;
				_instance.SetLobbyData(lobbyId, "lobby_name", "My Cool Lobby");
				DebugUtilities.PrintPeer("Lobby Created: " + lobbyId + ", Result: " + result);
			};
			_instance.LobbyJoinedSignal += (long lobby, long permissions, bool locked, long response) => {
				
				DebugUtilities.PrintPeer("Lobby Joined: " + lobby + ", Permissions: " + permissions + ", Locked: " + locked + ", Response: " + response);
			};
			_instance.CreateLobby(0, 2);
			

			
			DebugUtilities.PrintPeer("Steam Persona Name: " + name + ", State: " + state + ", App ID: " + appId);
		}
		else
		{
			DebugUtilities.PrintPeer("initialized SteamProcess, isRunning: false");
		}
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		_instance.RunCallbacks();
	}
}
