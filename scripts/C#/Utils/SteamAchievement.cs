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
///
/// A progressive achievement names the stat it counts against:
/// <code>new("ACH_ATTRITION", AchievementScope.Server, ProgressStat: "STAT_UNITS_LOST", ProgressTarget: 100)</code>
///
/// Deliberately carries no game concepts: this file and <see cref="SteamworksApi"/> are the reusable
/// half. What the game's achievements ARE lives in its own catalog — see <c>Achievements</c>, which
/// registers itself through <see cref="SteamworksApi.Catalog"/>.
/// </summary>
public sealed record SteamAchievement(
	string           ApiName,
	AchievementScope Scope          = AchievementScope.Client,
	string           ProgressStat   = null,
	int              ProgressTarget = 0)
{
	public bool IsProgressive => !string.IsNullOrWhiteSpace(ProgressStat) && ProgressTarget > 0;
}
