using Godot;

/// <summary>
/// Drives TargetRadarSweep.gdshader, the radar beam that sweeps over a selection target marker.
///
/// Shared by <see cref="ClickableSprite"/> and <see cref="ClickableTextureRect"/>, which draw the same
/// marker on two different node types and would otherwise each keep their own copy of this.
/// </summary>
public static class TargetRadar
{
    private static readonly Shader SweepShader = GD.Load<Shader>("res://assets/materials/shaders/TargetRadarSweep.gdshader");

    

    /// <summary>
    /// Godot wraps a shader's TIME at this many seconds. Read from the project rather than assumed, so
    /// the phase <see cref="Start"/> writes stays in the same window as the TIME it is subtracted from.
    /// </summary>
    private static double TimeRolloverSeconds
        => ProjectSettings.GetSetting("rendering/limits/time/time_rollover_secs", 3600.0).AsDouble();

    /// <summary>
    /// A material of this marker's own. Never one shared resource: the uniforms say whether THIS marker
    /// is sweeping and from what moment, so a shared material would make every target on the board agree
    /// with whichever one was configured last.
    /// </summary>
    public static ShaderMaterial CreateMaterial() => new ShaderMaterial { Shader = SweepShader };

    /// <summary>
    /// Starts the beam from a known angle. Phase-locked to now rather than left on the global clock, so
    /// that markers raised together sweep together and one raised on its own does not appear mid-turn.
    /// </summary>
    public static void Start(ShaderMaterial material)
    {
        if (material == null) return;
        // Time.GetTicksMsec and the shader's TIME are both real seconds since the engine started and
        // neither follows Engine.TimeScale, so they agree to within a frame.
        material.SetShaderParameter("start_time", (float)(Time.GetTicksMsec() / 1000.0 % TimeRolloverSeconds));
        material.SetShaderParameter("sweeping", true);
    }

    /// <summary>Holds the marker at whatever alpha its modulate gives it. See the subdued target style.</summary>
    public static void Stop(ShaderMaterial material)
    {
        material?.SetShaderParameter("sweeping", false);
    }
}
