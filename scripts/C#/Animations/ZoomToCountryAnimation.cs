using Godot;
using System.Threading.Tasks;

/// <summary>
/// Smoothly pans and zooms the camera to center on a country.
/// Pair with ReturnCameraAnimation in AfterAnimations to restore the camera afterwards.
/// </summary>
public class ZoomToCountryAnimation : ChangeEventAnimation
{
    private readonly int _countryId;
    private readonly float _targetZoom;

    /// <param name="countryId">The country to zoom to.</param>
    /// <param name="targetZoom">Zoom level to reach (higher = more zoomed in). Defaults to 0.6.</param>
    public ZoomToCountryAnimation(int countryId, float targetZoom = 0.6f)
    {
        _countryId = countryId;
        _targetZoom = targetZoom;
    }

    protected override async Task AnimateForTargetFaction()
    {
        Camera2D camera = InputManager.Current.Camera;
        CountryState country = CountryState.ForId(_countryId);
        Vector2 targetPosition = country.CountryScene.GlobalCenter;
        Vector2 targetZoomVec  = new Vector2(_targetZoom, _targetZoom);
        double  duration       = GameSettings.DurationMediumSeconds;

        DebugUtilities.PrintPeer($"{camera}");
        DebugUtilities.PrintPeer($"{camera.GetMultiplayerAuthority()}");
        DebugUtilities.PrintPeer("Zooming to country " + country.Label + " at position " + targetPosition + " with zoom " + _targetZoom);

        Tween tween = camera.CreateTween().SetParallel();
        tween.TweenProperty(camera, "position", targetPosition, duration)
             .SetTrans(Tween.TransitionType.Sine)
             .SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(camera, "zoom", targetZoomVec, duration)
             .SetTrans(Tween.TransitionType.Sine)
             .SetEase(Tween.EaseType.InOut);

        await camera.ToSignal(tween, Tween.SignalName.Finished);
        DebugUtilities.PrintPeer("Finished zooming to country " + country.Label);
        tween.Dispose();
    }
}
