using Godot;
using System.Collections.Generic;

/// <summary>
/// The faction artwork, names and state colours the menus draw seat grids from.
///
/// Extracted from MultiplayerLobby, which owned them privately, once a second screen needed the same
/// flags: the Skirmish screen's seat grid has to look like the lobby's, and two copies of a flag table
/// drift the moment the art changes. Same reasoning as <see cref="MenuSeedField"/>, which was pulled
/// out of these two screens for the same reason.
///
/// Consumed with <c>using static FactionMenuVisuals;</c> so every unqualified <c>AxisSet</c>,
/// <c>FlagPaths[...]</c> and <c>ColClaimed</c> keeps reading as it did — the alternative was rewriting
/// some fifty call sites in the largest and most network-sensitive file in the menus, for no gain.
///
/// Menu-only. <see cref="StaticGameData.FactionDataMap"/> is NOT available here: FactionDataList is
/// populated inside GameModeMultiplayerDefault.Init, i.e. only once a game exists — which is why
/// MainMenu's victory-screen preview carries its own label fallbacks too. What IS menu-safe is
/// <see cref="StaticGameData.PlayableFactions"/> and StaticGameData.FactionTeamForFaction.
/// </summary>
public static class FactionMenuVisuals
{
	/// <summary>
	/// Display order for a seat grid: the three Axis factions, then the three Allied ones.
	///
	/// Deliberately NOT turn order — <see cref="StaticGameData.PlayableFactions"/> is that, and anything
	/// building a faction LIST for the game (a seating assignment, an AI seat id) must use that instead,
	/// or the game's turn order silently follows a menu's layout choice.
	/// </summary>
	public static readonly List<Faction> AllPlayableFactions = new()
	{
		Faction.GERMANY, Faction.JAPAN, Faction.ITALY,
		Faction.UNITED_KINGDOM, Faction.SOVIET, Faction.UNITED_STATES
	};

	public static readonly HashSet<Faction> AxisSet = new()
		{ Faction.GERMANY, Faction.JAPAN, Faction.ITALY };

	public static readonly Dictionary<Faction, string> FlagPaths = new()
	{
		{ Faction.GERMANY,        "res://assets/factions/germany/Germany_Flag.png" },
		{ Faction.JAPAN,          "res://assets/factions/japan/Japan_Flag.png" },
		{ Faction.ITALY,          "res://assets/factions/italy/Italy_Flag.png" },
		{ Faction.UNITED_KINGDOM, "res://assets/factions/united_kingdom/UK_Flag.png" },
		{ Faction.SOVIET,         "res://assets/factions/soviet/Soviet_Flag.png" },
		{ Faction.UNITED_STATES,  "res://assets/factions/united_states/US_Flag.png" },
	};

	/// <summary>
	/// The "Random" pick's icon: the composite all-factions flag, already used elsewhere to mean
	/// "every faction at once" (see GameMessageDisplay's next-round flag).
	/// </summary>
	public const string RandomFlagPath = "res://assets/textures/Other/NextRoundFlag.png";

	public static readonly Dictionary<Faction, string> FactionNames = new()
	{
		{ Faction.GERMANY,        "Germany" },
		{ Faction.JAPAN,          "Japan" },
		{ Faction.ITALY,          "Italy" },
		{ Faction.UNITED_KINGDOM, "United Kingdom" },
		{ Faction.SOVIET,         "Soviet Union" },
		{ Faction.UNITED_STATES,  "United States" },
	};

	// ── Menu palette, matching scenes/menu/theme/MenuTheme.tres ───────────────
	public static readonly Color MenuBrass = new(0.851f, 0.678f, 0.361f);
	public static readonly Color MenuText  = new(0.929f, 0.898f, 0.82f);
	public static readonly Color MenuMuted = new(0.69f, 0.66f, 0.58f);

	/// <summary>The background of one list row (a save, a friend's lobby) inside a frosted card.</summary>
	public static StyleBoxFlat MenuRowStyle() => new()
	{
		BgColor = new Color(1, 1, 1, 0.035f),
		BorderColor = new Color(1, 1, 1, 0.1f),
		BorderWidthLeft = 3,
		CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3,
		CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
		ContentMarginLeft = 18, ContentMarginRight = 12,
		ContentMarginTop = 10, ContentMarginBottom = 10,
	};

	// ── Visual colours for button states ──────────────────────────────────────
	public static readonly Color ColClaimed     = Colors.White;                    // full colour + brass frame – claimed by this row's player
	public static readonly Color ColAvailable   = new(0.80f, 0.80f, 0.80f, 0.95f); // slightly dimmed – available & team-compatible
	public static readonly Color ColUnavailable = new(0.25f, 0.25f, 0.25f, 0.45f); // dark grey – wrong team
	public static readonly Color ColOtherOwned  = new(0.45f, 0.45f, 0.45f, 0.70f); // mid grey  – owned by another player

	/// <summary>
	/// Draws a brass frame round the flag while the button shows <see cref="ColClaimed"/>. Modulate
	/// changes don't redraw, so whoever restyles the button must QueueRedraw it.
	/// </summary>
	public static void AddClaimFrame(TextureButton button)
	{
		button.Draw += () =>
		{
			if (button.Modulate != ColClaimed || button.TextureNormal == null) return;
			Vector2 texture = button.TextureNormal.GetSize();
			float scale = Mathf.Min(button.Size.X / texture.X, button.Size.Y / texture.Y);
			Rect2 flag = new((button.Size - texture * scale) / 2, texture * scale);
			button.DrawRect(flag.Grow(3), MenuBrass, false, 2);
		};
	}

	// ── Player row sizing ─────────────────────────────────────────────────────
	/// <summary>
	/// Height of a LOBBY row's faction flags, and so of the row itself — nothing else in the row is
	/// taller. 60 rather than the original 96 because the player list is ~389px tall, so a full
	/// six-player lobby (6 x 60 + 5px separations = 385) has to fit without pushing the last rows
	/// behind a scrollbar.
	///
	/// The Skirmish grid does not use this: it has one seat per ROW rather than six flags per row, so
	/// its own height is set by how many rows fit that screen, not this one.
	/// </summary>
	public const int FlagHeight = 60;
}
