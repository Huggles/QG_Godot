using Godot;

/// <summary>
/// Where an asset lives this session. An enabled expansion may ship assets the base game lacks (the Air
/// Force sprite) or replace one it has, by putting it at the same path under its own assets/ folder;
/// the first enabled expansion that has it wins, then the base game's res://assets/.
/// </summary>
public static class ContentPaths
{
    /// <summary>The res:// path of <paramref name="assetPath"/>, given relative to assets/, e.g. "textures/Units/QGAirForce.png".</summary>
    public static string Resolve(string assetPath)
    {
        foreach (string id in GameOptions.EnabledExpansions)
        {
            string path = $"{ExpansionRegistry.Find(id).ResFolder}assets/{assetPath}";
            if (ResourceLoader.Exists(path)) return path;
        }
        return $"res://assets/{assetPath}";
    }
}
