using System.Collections.Generic;
using System.Linq;
using System.Reflection;

/// <summary>Who is allowed to award an achievement, enforced by <see cref="SteamworksApi.Unlock"/>.</summary>
public enum AchievementScope
{
	/// <summary>Any peer may unlock it from what it can see locally.</summary>
	Client,
	/// <summary>
	/// Only the authoritative peer may unlock it. Steamworks has no way for one machine to award an
	/// achievement to another, so this is a gate on the local player's own unlock, not a grant to others:
	/// a host earns it, a client watching the same state does not.
	/// </summary>
	Server,
}

/// <summary>
/// One achievement as configured in Steamworks. <paramref name="ApiName"/> must match the API Name
/// column there exactly — Steam answers a wrong name with a bare false that looks like any other refusal.
/// </summary>
public sealed record SteamAchievement(
	string           ApiName,
	AchievementScope Scope          = AchievementScope.Client,
	string           ProgressStat   = null,
	int              ProgressTarget = 0)
{
	public bool IsProgressive => !string.IsNullOrWhiteSpace(ProgressStat) && ProgressTarget > 0;
}

/// <summary>
/// Every achievement the game can award. Add a <c>public static readonly SteamAchievement</c> field here
/// and call <see cref="SteamworksApi.Unlock"/> with it; nothing else needs touching.
///
/// <code>
/// public static readonly SteamAchievement FirstBlood  = new("ACH_FIRST_BLOOD");
/// public static readonly SteamAchievement WarOfAttrition =
///     new("ACH_ATTRITION", AchievementScope.Server, ProgressStat: "STAT_UNITS_LOST", ProgressTarget: 100);
/// </code>
/// </summary>
public static class Achievements
{
	// ── Add achievements here ────────────────────────────────────────────────

	// ── Keep All last: it reads the fields above, and a static initialiser runs in declaration order.
	public static IReadOnlyList<SteamAchievement> All { get; } =
		typeof(Achievements)
			.GetFields(BindingFlags.Public | BindingFlags.Static)
			.Where(field => field.FieldType == typeof(SteamAchievement))
			.Select(field => (SteamAchievement)field.GetValue(null))
			.Where(achievement => achievement != null)
			.ToList();

	public static SteamAchievement Find(string apiName)
		=> All.FirstOrDefault(achievement => achievement.ApiName == apiName);
}
