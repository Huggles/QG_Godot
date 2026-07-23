using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// End-of-game screen. Reads the winner + per-faction / per-round scores from the static
/// PendingResult (captured before the game scene was torn down) and declares the winning team.
/// </summary>
public partial class VictoryScreen : Control
{
    /// <summary>Set by MultiplayerSession.BeginEndGame on every peer before switching scenes.</summary>
    public static GameResult PendingResult { get; set; }

    private static readonly Color AxisColor   = new Color("#d98a8a");
    private static readonly Color AlliesColor = new Color("#8aa9d9");
    private static readonly Color NeutralColor = new Color("#dddddd");

    public override void _Ready()
    {
        var mainMenu = GetNode<MenuPanelButton>("%MainMenuButton");
        mainMenu.ButtonText = "Main Menu";
        mainMenu.Pressed += OnMainMenuPressed;

        var title   = GetNode<RichTextLabel>("%TitleLabel");
        var results = GetNode<HBoxContainer>("%ResultsContainer");

        if (PendingResult == null)
        {
            title.Text = "[center][font_size=42][b]No Result[/b][/font_size][/center]";
            return;
        }

        BuildTitle(title, PendingResult);
        BuildColumn(results, PendingResult, FactionTeam.AXIS, AxisColor);
        BuildColumn(results, PendingResult, FactionTeam.ALLIES, AlliesColor);
    }

    private static void BuildTitle(RichTextLabel title, GameResult r)
    {
        Color winColor = r.WinningTeam == FactionTeam.AXIS ? AxisColor : AlliesColor;
        string winName = r.WinningTeam == FactionTeam.AXIS ? "AXIS" : "ALLIES";
        string banner  = $"[color={winColor.ToHtml(false)}]{winName} VICTORY[/color]";

        title.Text =
            $"[center][font_size=64][b]{banner}[/b][/font_size]\n" +
            $"[font_size=24][color={NeutralColor.ToHtml(false)}]" +
            $"Axis {r.AxisTotal}  —  Allies {r.AlliesTotal}\n" +
            $"Game ended: {r.EndReason} (Round {r.FinalRound})[/color][/font_size][/center]";
    }

    private static void BuildColumn(HBoxContainer parent, GameResult r, FactionTeam team, Color teamColor)
    {
        int teamTotal = team == FactionTeam.AXIS ? r.AxisTotal : r.AlliesTotal;
        string teamName = team == FactionTeam.AXIS ? "Axis" : "Allies";

        var column = new VBoxContainer();
        column.CustomMinimumSize = new Vector2(520, 0);
        column.AddThemeConstantOverride("separation", 12);
        parent.AddChild(column);

        var header = NewLabel();
        header.Text = $"[b][font_size=34][color={teamColor.ToHtml(false)}]{teamName}[/color][/font_size][/b]" +
                      $"  [font_size=28][color={NeutralColor.ToHtml(false)}]{teamTotal} pts[/color][/font_size]";
        column.AddChild(header);

        foreach (FactionResult faction in r.Factions.Where(f => f.Team == team).OrderByDescending(f => f.Total))
        {
            var row = NewLabel();
            row.Text = FactionRowText(faction, teamColor);
            column.AddChild(row);
        }
    }

    private static string FactionRowText(FactionResult faction, Color teamColor)
    {
        string breakdown = faction.PerRound.Count > 0
            ? string.Join("   ", faction.PerRound.Select(pr => $"R{pr.Round}: {pr.Points}"))
            : "no points scored";

        return
            $"[font_size=24][b][color={teamColor.ToHtml(false)}]{PrettyName(faction.Faction)}[/color][/b]  " +
            $"[color={NeutralColor.ToHtml(false)}]{faction.Total} pts[/color][/font_size]\n" +
            $"[font_size=16][color=#999999]{breakdown}[/color][/font_size]";
    }

    private static RichTextLabel NewLabel()
    {
        var label = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        label.CustomMinimumSize = new Vector2(500, 0);
        return label;
    }

    /// <summary>Turns an enum name like UNITED_STATES into "United States".</summary>
    private static string PrettyName(Faction faction)
    {
        IEnumerable<string> words = faction.ToString()
            .Split('_')
            .Select(w => w.Length == 0 ? w : char.ToUpper(w[0]) + w.Substring(1).ToLower());
        return string.Join(" ", words);
    }

    private void OnMainMenuPressed()
    {
        // Cleanly leave any multiplayer session so a fresh game can be hosted/joined.
        if (Multiplayer.MultiplayerPeer != null)
            Multiplayer.MultiplayerPeer = null;
        GetTree().ChangeSceneToFile("res://scenes/menu/MainMenu.tscn");
    }
}
