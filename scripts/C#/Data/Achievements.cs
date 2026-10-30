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

	// ── Easy ─────────────────────────────────────────────────────────────────────────────────

	/// <summary>First Victory — win a game.</summary>
	public static readonly SteamAchievement FirstVictory = new("FIRST_VICTORY");
	/// <summary>Win a game with Germany among your factions; likewise for the five below.</summary>
	public static readonly SteamAchievement VictoryGermany       = new("VICTORY_GERMANY");
	public static readonly SteamAchievement VictoryUnitedKingdom = new("VICTORY_UNITED_KINGDOM");
	public static readonly SteamAchievement VictoryJapan         = new("VICTORY_JAPAN");
	public static readonly SteamAchievement VictorySoviet        = new("VICTORY_SOVIET");
	public static readonly SteamAchievement VictoryItaly         = new("VICTORY_ITALY");
	public static readonly SteamAchievement VictoryUnitedStates  = new("VICTORY_UNITED_STATES");
	/// <summary>First Blood — make the first elimination of a game.</summary>
	public static readonly SteamAchievement FirstBlood = new("FIRST_BLOOD");
	/// <summary>D-Day — a United Kingdom or United States Army in Western Europe.</summary>
	public static readonly SteamAchievement DDay = new("D_DAY");

	// ── Medium ───────────────────────────────────────────────────────────────────────────────

	/// <summary>Decisive Victory — win on the 30-point lead rather than the round limit.</summary>
	public static readonly SteamAchievement DecisiveVictory = new("DECISIVE_VICTORY");
	/// <summary>Operation Barbarossa — a German Army in Moscow.</summary>
	public static readonly SteamAchievement OperationBarbarossa = new("OPERATION_BARBAROSSA");
	/// <summary>War of Attrition — eliminate 100 enemy units, across any number of games.</summary>
	public static readonly SteamAchievement WarOfAttrition = new("WAR_OF_ATTRITION", ProgressStat: "STAT_ENEMY_UNITS_ELIMINATED", ProgressTarget: 100);

	// ── Hard ─────────────────────────────────────────────────────────────────────────────────

	/// <summary>Lightning War — win on the 30-point lead by the end of round <see cref="LightningWarLastRound"/>.</summary>
	public static readonly SteamAchievement LightningWar = new("LIGHTNING_WAR");
	/// <summary>Against All Odds — win after trailing by <see cref="AgainstAllOddsDeficit"/> or more at the end of a round.</summary>
	public static readonly SteamAchievement AgainstAllOdds = new("AGAINST_ALL_ODDS");
	/// <summary>Downfall — a United States Army in Japan.</summary>
	public static readonly SteamAchievement Downfall = new("DOWNFALL");
	/// <summary>Veteran Commander — win 10 games.</summary>
	public static readonly SteamAchievement VeteranCommander = new("VETERAN_COMMANDER", ProgressStat: "STAT_GAMES_WON", ProgressTarget: 10);
	/// <summary>Doolittle Raid — make the first elimination of a game as the United States, who move last each round.</summary>
	public static readonly SteamAchievement FirstBloodUs = new("FIRST_BLOOD_US");

	// ── Economic warfare: force one enemy faction to discard enough cards in a single round ──
	// The Soviets have no Economic Warfare card, so no achievement.

	public static readonly SteamAchievement EconomicWarfareGermany       = new("ECONOMIC_WARFARE_GERMANY");
	public static readonly SteamAchievement EconomicWarfareUnitedKingdom = new("ECONOMIC_WARFARE_UNITED_KINGDOM");
	public static readonly SteamAchievement EconomicWarfareJapan         = new("ECONOMIC_WARFARE_JAPAN");
	public static readonly SteamAchievement EconomicWarfareItaly         = new("ECONOMIC_WARFARE_ITALY");
	public static readonly SteamAchievement EconomicWarfareUnitedStates  = new("ECONOMIC_WARFARE_UNITED_STATES");
	/// <summary>Economic Stranglehold — one faction forces enemies to discard <see cref="EconomicStrangleholdCards"/> cards in one game.</summary>
	public static readonly SteamAchievement EconomicStranglehold = new("ECONOMIC_STRANGLEHOLD");
	public const int EconomicStrangleholdCards = 10;

	// ── Supply: an enemy loses this many units to lack of supply in one turn ──────────────────

	/// <summary>Cut Off — one unit.</summary>
	public static readonly SteamAchievement SupplyLosses1 = new("SUPPLY_LOSSES_1");
	/// <summary>Encirclement — two units.</summary>
	public static readonly SteamAchievement SupplyLosses2 = new("SUPPLY_LOSSES_2");
	/// <summary>The Pocket — three units.</summary>
	public static readonly SteamAchievement SupplyLosses3 = new("SUPPLY_LOSSES_3");

	/// <summary>Entry n-1 is the achievement for n units lost in one turn.</summary>
	public static readonly IReadOnlyList<SteamAchievement> SupplyLosses = new[] { SupplyLosses1, SupplyLosses2, SupplyLosses3 };

	/// <summary>About one bot game in twenty-five ends on the lead this early (sim/runs, 36k standard games).</summary>
	public const int LightningWarLastRound = 8;
	public const int AgainstAllOddsDeficit = 20;

	private static readonly Dictionary<(Faction, Country), SteamAchievement> ArmyPlacements = new()
	{
		[(Faction.GERMANY,        Country.Australia)]     = SouthernCrossAustralia,
		[(Faction.ITALY,          Country.Australia)]     = NewRomanEmpire,
		[(Faction.JAPAN,          Country.WesternEurope)] = SunRisesOverEurope,
		[(Faction.UNITED_KINGDOM, Country.Vladivostok)]   = LongWayFromLondon,
		[(Faction.UNITED_STATES,  Country.Siberia)]       = AlongTheTransSiberian,
		[(Faction.SOVIET,         Country.Australia)]     = RedArmyDownUnder,

		[(Faction.UNITED_KINGDOM, Country.WesternEurope)] = DDay,
		[(Faction.UNITED_STATES,  Country.WesternEurope)] = DDay,
		[(Faction.GERMANY,        Country.Moscow)]        = OperationBarbarossa,
		[(Faction.UNITED_STATES,  Country.Japan)]         = Downfall,
	};

	/// <summary>The achievement for landing this faction's Army here, or null for every ordinary deploy.</summary>
	public static SteamAchievement ArmyIn(Faction faction, Country country)
		=> ArmyPlacements.GetValueOrDefault((faction, country));

	private static readonly Dictionary<Faction, SteamAchievement> FactionVictories = new()
	{
		[Faction.GERMANY]        = VictoryGermany,
		[Faction.UNITED_KINGDOM] = VictoryUnitedKingdom,
		[Faction.JAPAN]          = VictoryJapan,
		[Faction.SOVIET]         = VictorySoviet,
		[Faction.ITALY]          = VictoryItaly,
		[Faction.UNITED_STATES]  = VictoryUnitedStates,
	};

	/// <summary>The achievement for winning with this faction, or null for a minor faction.</summary>
	public static SteamAchievement VictoryAs(Faction faction) => FactionVictories.GetValueOrDefault(faction);

	public sealed record DiscardGoal(SteamAchievement Achievement, int Cards);

	// One card from hand per turn, so each goal has to fit one play plus any table cards. The UK's best is Bomber Command's 4.
	private static readonly Dictionary<Faction, DiscardGoal> DiscardGoals = new()
	{
		[Faction.GERMANY]        = new(EconomicWarfareGermany,       5),
		[Faction.UNITED_KINGDOM] = new(EconomicWarfareUnitedKingdom, 4),
		[Faction.JAPAN]          = new(EconomicWarfareJapan,         5),
		[Faction.ITALY]          = new(EconomicWarfareItaly,         3),
		[Faction.UNITED_STATES]  = new(EconomicWarfareUnitedStates,  5),
	};

	/// <summary>What this faction must make one enemy discard in a round, or null when it has no such achievement.</summary>
	public static DiscardGoal DiscardGoalFor(Faction faction) => DiscardGoals.GetValueOrDefault(faction);

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
