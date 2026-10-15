using Godot;
using System.Threading.Tasks;

/// <summary>
/// Flies an Air Force from one space to another: the same piece lifts out of its old slot and glides to
/// its new one, rather than burning in one place and dropping in elsewhere the way a remove and a
/// deploy would.
/// </summary>
public class MoveAirForceAnimation : ChangeEventAnimation
{
    private readonly int _unitId;
    private readonly int _fromCountryId;
    private readonly int _toCountryId;

    public MoveAirForceAnimation(int unitId, int fromCountryId, int toCountryId)
    {
        _unitId = unitId;
        _fromCountryId = fromCountryId;
        _toCountryId = toCountryId;
    }

    protected override async Task AnimateForTargetFaction()
    {
        UnitScene unitScene = UnitState.ForId(_unitId).UnitScene;
        if (unitScene == null) return;

        // Book the destination slot first (AddUnit re-parents and positions it there), then put the
        // piece back where it was on screen and fly it over. Moving to the space it is already in — a
        // marshal that only triggers a reaction — leaves the slot it holds and flies nowhere.
        Vector2 start = unitScene.GlobalPosition;
        CountryState.ForId(_fromCountryId).CountryScene.DetachAirForce(unitScene);
        CountryState.ForId(_toCountryId).CountryScene.AddUnit(unitScene);
        Vector2 end = unitScene.GlobalPosition;
        if (start == end) return;

        unitScene.GlobalPosition = start;
        Tween tween = unitScene.CreateTween();
        tween.TweenProperty(unitScene, "global_position", end, GameSettings.DurationMediumSeconds)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        await unitScene.ToSignal(tween, Tween.SignalName.Finished);
        tween.Dispose();

        // Pinned rather than trusted to the tween's last frame, which a zero-length (fast-forward) run skips.
        unitScene.GlobalPosition = end;
    }
}
