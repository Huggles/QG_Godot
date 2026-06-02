using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class PlayerInfoDisplay : PanelContainer, LoadableUI
{
    public static PlayerInfoDisplay Instance;

    private Label _roleLabel;
    private Label _factionsLabel;
    private VBoxContainer _container;
    
    private EventBus.FactionsAssignedEventHandler _onFactionsAssigned;

    public override void _Ready()
    {
        DebugUtilities.PrintPeer("PlayerInfoDisplay _Ready called");
        Instance = this;
        
        // Hide by default until LoadUI is called
        Hide();
    }

    public override void _ExitTree()
    {
        // Unsubscribe from events
        if (EventBus.Instance != null && _onFactionsAssigned != null)
        {
            EventBus.Instance.FactionsAssigned -= _onFactionsAssigned;
        }
    }

    public void LoadUI()
    {
        _container = GetNode<VBoxContainer>("%Container");
        _roleLabel = GetNode<Label>("%RoleLabel");
        _factionsLabel = GetNode<Label>("%FactionsLabel");

        // Subscribe to faction assignment event
        _onFactionsAssigned = () =>
        {
            if (!IsInstanceValid(this) || !IsInsideTree()) return;
            UpdateDisplay();
        };
        EventBus.Instance.FactionsAssigned += _onFactionsAssigned;

        UpdateDisplay();
        Show();
    }

    private void UpdateDisplay()
    {
        if (_roleLabel == null || _factionsLabel == null)
            return;

        // Determine if player is host
        bool isHost = PlayerFactionRegistry.IsLocalPlayerHost();
        _roleLabel.Text = isHost ? "Host" : "Client";

        // Get player's factions
        List<Faction> factions = PlayerFactionRegistry.GetLocalPlayerFactions();
        
        if (factions.Count == 0)
        {
            _factionsLabel.Text = "No factions assigned";
        }
        else
        {
            // Format faction names nicely
            string factionNames = string.Join(", ", factions.Select(f => FactionToDisplayName(f)));
            _factionsLabel.Text = $"Playing: {factionNames}";
        }
    }

    private string FactionToDisplayName(Faction faction)
    {
        return faction switch
        {
            Faction.GERMANY => "Germany",
            Faction.ITALY => "Italy",
            Faction.JAPAN => "Japan",
            Faction.SOVIET => "Soviet Union",
            Faction.UNITED_KINGDOM => "UK",
            Faction.UNITED_STATES => "USA",
            _ => faction.ToString()
        };
    }

    public new void Show()
    {
        Visible = true;
    }

    public new void Hide()
    {
        Visible = false;
    }
}
