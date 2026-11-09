using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Top-left list of the people in the game, one row per player (not per faction), each with the flags
/// of the factions they play and their ping or connection state. Fed by <see cref="ConnectionMonitor"/>'s player table and toggled from the
/// mini menu's visibility list; empty, and so hidden, outside a multiplayer game.
/// </summary>
public partial class PlayersPanel : PanelContainer
{
    public static PlayersPanel Current { get; private set; }

    private static readonly Color GoodColor = new("#7fd67f");
    private static readonly Color FairColor = new("#e6c35c");
    private static readonly Color BadColor = new("#e57373");
    private static readonly Color QuietColor = new("#a0a0a0");

    /// <summary>Three columns (name, flags, status), so each column lines up across players.</summary>
    private GridContainer Rows => GetNode<GridContainer>("%Rows");

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
        foreach (PlayerConnectionRow row in table) AddRow(row);
    }

    /// <summary>One cell per grid column; the flags column takes the spare width so the status stays right.</summary>
    private void AddRow(PlayerConnectionRow row)
    {
        Label name = new() { Text = row.Name };
        name.AddThemeFontSizeOverride("font_size", 14);
        Rows.AddChild(name);

        HBoxContainer flags = BuildFlags(row.SeatId);
        flags.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        Rows.AddChild(flags);

        (string text, Color color) = Status(row);
        Label status = new() { Text = text, HorizontalAlignment = HorizontalAlignment.Right };
        status.AddThemeFontSizeOverride("font_size", 14);
        status.AddThemeColorOverride("font_color", color);
        Rows.AddChild(status);
    }

    /// <summary>
    /// The seat's factions as small flags, in turn order. Read from this peer's own registry: every peer
    /// holds the same seat-to-faction assignment, so the host's table need not carry it.
    /// </summary>
    private static HBoxContainer BuildFlags(int seatId)
    {
        HBoxContainer flags = new() { MouseFilter = MouseFilterEnum.Ignore };
        flags.AddThemeConstantOverride("separation", 3);
        foreach (Faction faction in PlayerFactionRegistry.GetFactionsForPeerId(seatId).OrderBy(f => (int)f))
        {
            if (StaticGameData.FactionDataMap.GetValueOrDefault(faction)?.FlagTexture is not { } texture) continue;
            flags.AddChild(new TextureRect
            {
                Texture = texture,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = new Vector2(24, 12),
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
                MouseFilter = MouseFilterEnum.Ignore,
            });
        }
        return flags;
    }

    private static (string, Color) Status(PlayerConnectionRow row)
    {
        if (row.IsHost) return ("host", QuietColor);
        return row.State switch
        {
            SeatState.Bot => ("bot", QuietColor),
            SeatState.Rejoining => ("rejoining…", FairColor),
            SeatState.Absent => (row.SecondsLeft > 0 ? $"reconnecting… {row.SecondsLeft}s" : "disconnected", BadColor),
            SeatState.Unstable => ("unstable", FairColor),
            _ => ($"{row.PingMs} ms", row.PingMs < 80 ? GoodColor : row.PingMs < 200 ? FairColor : BadColor),
        };
    }
}
