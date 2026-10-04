using Godot;

/// <summary>The running build's identity, as set in project.godot.</summary>
public static class BuildInfo
{
	/// <summary>Host and client must match exactly to play together; see RejoinService.Hello.</summary>
	public static string Version =>
		ProjectSettings.GetSetting("application/config/version", "").AsString();
}
