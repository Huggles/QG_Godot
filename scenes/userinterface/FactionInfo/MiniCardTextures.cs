using Godot;
using System.Collections.Generic;

/// <summary>
/// The little cards fanned out of the top of a faction flag: flat, hard-bordered icons, because the real
/// card art shrunk to 15 pixels wide is mush. A status card looks the same for every faction
/// (<c>StatusMini.svg</c>); a face-down response card is a card back in the faction's player colour,
/// <c>{Faction}_CardBackMini.svg</c> next to its card art, or the neutral <c>CardBackMini.svg</c> for a
/// faction without its own (one an expansion adds). Drawn at three times the size the fan shows them, so
/// they stay sharp at the 120% banner size and on a hi-dpi screen.
/// </summary>
public static class MiniCardTextures
{
	private const string FactionBackPath = "res://assets/factions/{0}/cards/{1}_CardBackMini.svg";
	private const string GenericBackPath = "res://assets/textures/Other/Icons/CardBackMini.svg";

	/// <summary>A face-up status card; the same for every faction.</summary>
	public static readonly Texture2D Status = GD.Load<Texture2D>("res://assets/textures/Other/Icons/StatusMini.svg");

	private static readonly Dictionary<Faction, Texture2D> BackCache = new();

	/// <summary><paramref name="faction"/>'s card back in its player colour, as a face-down response card shows.</summary>
	public static Texture2D Back(Faction faction)
	{
		if (BackCache.TryGetValue(faction, out Texture2D texture)) return texture;
		FactionData data = StaticGameData.FactionDataMap[faction];
		string path = string.Format(FactionBackPath, data.factionName, data.factionNameCapitalized);
		return BackCache[faction] = GD.Load<Texture2D>(ResourceLoader.Exists(path) ? path : GenericBackPath);
	}
}
