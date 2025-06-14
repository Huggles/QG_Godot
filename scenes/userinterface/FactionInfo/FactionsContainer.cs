using Godot;
using System;
using System.Collections.Generic;

public partial class FactionsContainer : Control
{
    public static FactionsContainer Instance;

    private HBoxContainer HorizontalContainer => GetNode<HBoxContainer>("%FactionsHorizontalContainer");

    private Dictionary<Faction, FactionInfoRow> factionInfoNodes = new();
    private PackedScene rowScene;

    private static readonly string ChangeEventRowScenePath = "res://scenes/userinterface/FactionInfo/FactionInfoRow.tscn";

    public override void _Ready()
    {
        Instance = this;
        EventBus.Instance.PlayerJoined += OnPlayerJoined;
        EventBus.Instance.PlayerLeft += OnPlayerLeft;
        rowScene = GD.Load<PackedScene>(ChangeEventRowScenePath);
        InitChildElements();
    }

    private void InitChildElements()
    {
        foreach (Node child in HorizontalContainer.GetChildren())
        {
            HorizontalContainer.RemoveChild(child);
            child.QueueFree();
        }

        factionInfoNodes.Clear();

        foreach (Faction faction in Enum.GetValues(typeof(Faction)))
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