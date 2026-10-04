using Godot;

/// <summary>
/// Top-left list of the people in the game, one row per player (not per faction), each with their
/// ping or connection state. Fed by <see cref="ConnectionMonitor"/>'s player table and toggled from the
/// mini menu's visibility list; empty, and so hidden, outside a multiplayer game.
/// </summary>
public partial class PlayersPanel : PanelContainer
{
    public static PlayersPanel Current { get; private set; }

    private static readonly Color GoodColor = new("#7fd67f");
    private static readonly Color FairColor = new("#e6c35c");
    private static readonly Color BadColor = new("#e57373");
    private static readonly Color QuietColor = new("#a0a0a0");

    private VBoxContainer Rows => GetNode<VBoxContainer>("%Rows");

    public override void _Ready()
    {
        if (!SessionIdentity.IsLocalAuthority(this))
        {
            QueueFree();
            return;
        }

        Current = this;
        if (ConnectionMonitor.Instance != null) ConnectionMonitor.Instance.TableChanged += Refresh;
        Refresh();
    }

    public override void _ExitTree()
    {
        if (Current == this) Current = null;
        if (ConnectionMonitor.Instance != null) ConnectionMonitor.Instance.TableChanged -= Refresh;
    }

    /// <summary>Rebuilt whole on every table: a handful of rows, a few times a minute.</summary>
    public void Refresh()
    {
        var table = ConnectionMonitor.Instance?.Rows;
        Visible = GameSettings.ShowPlayersPanel && table is { Count: > 0 };
        if (!Visible) return;

        foreach (Node child in Rows.GetChildren())
        {
            Rows.RemoveChild(child);
            child.QueueFree();
        }
        foreach (PlayerConnectionRow row in table) Rows.AddChild(BuildRow(row));
    }

    private static HBoxContainer BuildRow(PlayerConnectionRow row)
    {
        HBoxContainer line = new();
        line.AddThemeConstantOverride("separation", 16);

        Label name = new() { Text = row.Name, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        name.AddThemeFontSizeOverride("font_size", 14);
        line.AddChild(name);

        (string text, Color color) = Status(row);
        Label status = new() { Text = text, HorizontalAlignment = HorizontalAlignment.Right };
        status.AddThemeFontSizeOverride("font_size", 14);
        status.AddThemeColorOverride("font_color", color);
        line.AddChild(status);
        return line;
    }

    private static (string, Color) Status(PlayerConnectionRow row)
    {
        if (row.IsHost) return ("host", QuietColor);
        return row.State switch
        {
            SeatState.Bot => ("bot", QuietColor),
            SeatState.Absent => (row.SecondsLeft > 0 ? $"reconnecting… {row.SecondsLeft}s" : "disconnected", BadColor),
            SeatState.Unstable => ("unstable", FairColor),
            _ => ($"{row.PingMs} ms", row.PingMs < 80 ? GoodColor : row.PingMs < 200 ? FairColor : BadColor),
        };
    }
}
