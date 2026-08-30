using Godot;
using System.Threading.Tasks;

/// <summary>
/// Smoothly returns the camera to the position and zoom it had when this animation was constructed.
/// Because Apply evaluates BeforeAnimations and AfterAnimations upfront (before any animation
/// runs), constructing this in AfterAnimations captures the pre-zoom state automatically.
/// </summary>
public class ReturnCameraAnimation : ChangeEventAnimation
{
    private readonly Vector2 _savedPosition;
    private readonly Vector2 _savedZoom;

    /// <summary>False when there was no camera to read at construction, which makes this a no-op.</summary>
    private readonly bool _captured;

    public ReturnCameraAnimation()
    {
        // Null-safe on purpose: this constructor runs inside ChangeEvent.ApplyMutation, where a throw
        // between the mutation and BroadCast() is reported as peer divergence. A Player scene freed out
        // from under an in-flight event (leaving the game, loading another) must degrade to "no camera
        // to restore", not take the change event down with it.
        Camera2D camera = InputManager.Current?.Camera;
        if (camera == null) return;

        // Global to match ZoomToCountryAnimation and InputManager's bounds clamp — the camera's local
        // position is offset by Game/Players and means nothing on its own.
        _savedPosition = camera.GlobalPosition;
        _savedZoom     = camera.Zoom;
        _captured      = true;
    }

    protected override async Task AnimateForTargetFaction()
    {
        Camera2D camera = InputManager.Current?.Camera;
        // Re-checked rather than trusted from the constructor: the queue runs this later, and the scene
        // can have gone away in between.
        if (!_captured || camera == null) return;

        double duration = GameSettings.DurationMediumSeconds;

        Tween tween = camera.CreateTween().SetParallel();
        tween.TweenProperty(camera, "global_position", _savedPosition, duration)
             .SetTrans(Tween.TransitionType.Sine)
             .SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(camera, "zoom", _savedZoom, duration)
             .SetTrans(Tween.TransitionType.Sine)
             .SetEase(Tween.EaseType.InOut);

        await camera.ToSignal(tween, Tween.SignalName.Finished);
        tween.Dispose();
    }
}
