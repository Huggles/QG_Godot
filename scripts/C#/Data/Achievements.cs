using System.Collections.Generic;
using System.Linq;
using System.Reflection;

/// <summary>
/// Every achievement this game can award, mirroring the Steamworks partner page. Add a
/// <c>public static readonly SteamAchievement</c> field and call <see cref="SteamworksApi.Unlock"/> with
/// it; <see cref="All"/> picks it up on its own, and <c>AchievementTracker</c> registers the list so the
/// Steam layer can check the names against Steamworks at startup.
/// </summary>
public static class Achievements
{
	// ── "An army in a place it has no business being" — awarded by AchievementTracker ─────────

	/// <summary>The Southern Cross is OURS — a German Army in Australia.</summary>
	public static readonly SteamAchievement SouthernCrossAustralia = new("SOUTHERN_CROSS_AUSTRALIA");
	/// <summary>The New Roman Empire — an Italian Army in Australia.</summary>
	public static readonly SteamAchievement NewRomanEmpire = new("NEW_ROMAN_EMPIRE");
	/// <summary>The Sun Rises Over Europe — a Japanese Army in Western Europe.</summary>
	public static readonly SteamAchievement SunRisesOverEurope = new("SUN_RISES_OVER_EUROPE");
	/// <summary>A Very Long Way from London — a United Kingdom Army in Vladivostok.</summary>
	public static readonly SteamAchievement LongWayFromLondon = new("LONG_WAY_FROM_LONDON");
	/// <summary>Along the Trans-Siberian — a United States Army in Siberia.</summary>
	public static readonly SteamAchievement AlongTheTransSiberian = new("ALONG_THE_TRANS_SIBERIAN");
	/// <summary>The Red Army Down Under — a Soviet Army in Australia.</summary>
	public static readonly SteamAchievement RedArmyDownUnder = new("RED_ARMY_DOWN_UNDER");

	private static readonly Dictionary<(Faction, Country), SteamAchievement> ArmyPlacements = new()
	{
		[(Faction.GERMANY,        Country.Australia)]     = SouthernCrossAustralia,
		[(Faction.ITALY,          Country.Australia)]     = NewRomanEmpire,
		[(Faction.JAPAN,          Country.WesternEurope)] = SunRisesOverEurope,
		[(Faction.UNITED_KINGDOM, Country.Vladivostok)]   = LongWayFromLondon,
		[(Faction.UNITED_STATES,  Country.Siberia)]       = AlongTheTransSiberian,
		[(Faction.SOVIET,         Country.Australia)]     = RedArmyDownUnder,
	};

	/// <summary>The achievement for landing this faction's Army here, or null for every ordinary deploy.</summary>
	public static SteamAchievement ArmyIn(Faction faction, Country country)
		=> ArmyPlacements.GetValueOrDefault((faction, country));

	// Declared last on purpose: static initialisers run in source order, so the fields it reflects over
	// have to already be assigned.
	public static IReadOnlyList<SteamAchievement> All { get; } =
		typeof(Achievements)
			.GetFields(BindingFlags.Public | BindingFlags.Static)
			.Where(field => field.FieldType == typeof(SteamAchievement))
			.Select(field => (SteamAchievement)field.GetValue(null))
			.Where(achievement => achievement != null)
			.ToList();
}
