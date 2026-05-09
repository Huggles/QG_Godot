using Godot;
using System;
using System.Collections.Generic;

public partial class FactionsContainer : Control, LoadableUI
{
    public static FactionsContainer Instance;

    private HBoxContainer HorizontalContainer => GetNode<HBoxContainer>("%FactionsHorizontalContainer");

    private Dictionary<Faction, FactionInfoRow> factionInfoNodes = new();
    private PackedScene rowScene;

    private static readonly string FactionInfoRowScenePath = "res://scenes/userinterface/FactionInfo/FactionInfoRow.tscn";

    private EventBus.FactionsAssignedEventHandler _onFactionsAssigned;

    public override void _Ready()
    {
        Instance = this;
        
        // Hide by default until LoadUI is called
        Hide();
        
        EventBus.Emit(EventBus.SignalName.UserInterfaceLoaded, "FactionsContainer");    
    }

    public override void _ExitTree()
    {
        // Unsubscribe from events
        if (EventBus.Instance != null)
        {
            EventBus.Instance.PlayerJoined -= OnPlayerJoined;
            EventBus.Instance.PlayerLeft -= OnPlayerLeft;
            
            if (_onFactionsAssigned != null)
            {
                EventBus.Instance.FactionsAssigned -= _onFactionsAssigned;
            }
        }
    }

    public void LoadUI()
    {
        EventBus.Instance.PlayerJoined += OnPlayerJoined;
        EventBus.Instance.PlayerLeft += OnPlayerLeft;
        
        // Subscribe to faction assignment event
        _onFactionsAssigned = () =>
        {
            if (!IsInstanceValid(this) || !IsInsideTree()) return;
            InitChildElements();
        };
        EventBus.Instance.FactionsAssigned += _onFactionsAssigned;
        
        rowScene = GD.Load<PackedScene>(FactionInfoRowScenePath);
        InitChildElements();
        
        // Show faction info during gameplay
        Show();
    }
    

    private void InitChildElements()
    {
        foreach (Node child in HorizontalContainer.GetChildren())
        {
            HorizontalContainer.RemoveChild(child);
            child.QueueFree();
        }

        factionInfoNodes.Clear();

        // Only show factions controlled by the local player
        List<Faction> playerFactions = PlayerFactionRegistry.GetLocalPlayerFactions();
        
        // If no factions assigned yet, don't show any rows
        if (playerFactions.Count == 0)
        {
            return;
        }

        foreach (Faction faction in playerFactions)
        {
            FactionInfoRow rowInstance = rowScene.Instantiate<FactionInfoRow>();
            rowInstance.Faction = faction;
            HorizontalContainer.AddChild(rowInstance);
            factionInfoNodes[faction] = rowInstance;
        }
    }

    private void OnPlayerJoined()
    {
        InitChildElements();
    }

    private void OnPlayerLeft()
    {
        InitChildElements();
    }

    public FactionInfoRow GetFactionInfoNodeForFaction(Faction faction)
    {
        return factionInfoNodes[faction];
    }
}