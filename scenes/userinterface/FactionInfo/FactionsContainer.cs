using Godot;
using System;
using System.Collections.Generic;

public partial class FactionsContainer : Control
{
    public static FactionsContainer Current;

    private HBoxContainer HorizontalContainer => GetNode<HBoxContainer>("%FactionsHorizontalContainer");
    private Dictionary<Faction, FactionInfoRow> factionInfoNodes = new();    

    public override void _Ready()
    {   
        if(GetMultiplayerAuthority() == Multiplayer.GetUniqueId())
        {
            DebugUtilities.PrintPeerFinest($"Setting up FactionsContainer for local player: {GetMultiplayerAuthority()}");
            Current = this;
            EventBus.Instance.PlayerJoined += InitChildElements;
            EventBus.Instance.PlayerLeft += InitChildElements;
            EventBus.Instance.FactionsAssigned += InitChildElements;        
            EventBus.Instance.GameSessionStarted += InitChildElements;
            EventBus.Instance.UserInterfaceReady += () => {
                DebugUtilities.PrintPeerFinest("FactionsContainer received UserInterfaceReady signal, initializing child elements");
                InitChildElements();
            };
        }
        DebugUtilities.PrintPeerFinest($"FactionsContainer ready: {GetMultiplayerAuthority()}");  
    }

    public override void _ExitTree()
    {
        // Unsubscribe from events
        if (EventBus.Instance != null)
        {
            EventBus.Instance.PlayerJoined -= InitChildElements;
            EventBus.Instance.PlayerLeft -= InitChildElements;
            EventBus.Instance.FactionsAssigned -= InitChildElements;        
            EventBus.Instance.GameSessionStarted -= InitChildElements;
        }
    }
    

    private void InitChildElements()
    {
        DebugUtilities.PrintPeerFinest("FactionsContainer initializing child elements");
        foreach (Node child in HorizontalContainer.GetChildren())
        {
            HorizontalContainer.RemoveChild(child);
            child.QueueFree();
        }

        factionInfoNodes.Clear();

        // Only show factions controlled by the local player
        List<Faction> playerFactions = StaticGameData.PlayableFactions;
        
        // If no factions assigned yet, don't show any rows
        if (playerFactions.Count == 0)
        {
            return;
        }

        foreach (Faction faction in playerFactions)
        {
            FactionInfoRow rowInstance = AssetRepository.FactionInfoRowScenePacked.Instantiate<FactionInfoRow>();
            rowInstance.Faction = faction;
            HorizontalContainer.AddChild(rowInstance);
            factionInfoNodes[faction] = rowInstance;
        }
    }

    public FactionInfoRow GetFactionInfoNodeForFaction(Faction faction)
    {
        return factionInfoNodes[faction];
    }
}