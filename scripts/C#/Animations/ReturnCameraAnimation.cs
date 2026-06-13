using Godot;
using System.Threading.Tasks;

/// <summary>
/// Smoothly returns the camera to the position and zoom it had when this animation was constructed.
/// Because ApplyChange evaluates BeforeAnimations and AfterAnimations upfront (before any animation
/// runs), constructing this in AfterAnimations captures the pre-zoom state automatically.
/// </summary>
public class ReturnCameraAnimation : ChangeEventAnimation
{
    private readonly Vector2 _savedPosition;
    private readonly Vector2 _savedZoom;

    public ReturnCameraAnimation()
    {
        Camera2D camera = InputManager.Current.Camera;
        _savedPosition = camera.Position;
        _savedZoom     = camera.Zoom;
    }

    public override async Task Execute()
    {
        Camera2D camera  = InputManager.Current.Camera;
        double   duration = GameSettings.AnimationDurationSeconds * 3;

        Tween tween = camera.CreateTween().SetParallel();
        tween.TweenProperty(camera, "position", _savedPosition, duration)
             .SetTrans(Tween.TransitionType.Sine)
             .SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(camera, "zoom", _savedZoom, duration)
             .SetTrans(Tween.TransitionType.Sine)
             .SetEase(Tween.EaseType.InOut);

        await camera.ToSignal(tween, Tween.SignalName.Finished);
        tween.Dispose();
    }
}
