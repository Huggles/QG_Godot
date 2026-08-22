using Godot;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Plays the shrink tween on a unit being removed, then calls CountryScene.RemoveUnit.
/// countryId must be captured before ExecuteAsync runs (UnitState.CountryId is -1 afterwards).
/// The signal handler OnUnitRemovedFromCountry is a no-op so this animation owns scene cleanup.
/// </summary>
public class RemoveUnitAnimation : ChangeEventAnimation
{
    private readonly int _unitId;
    private readonly int _countryId;

    public RemoveUnitAnimation(int unitId, int countryId)
    {
        _unitId    = unitId;
        _countryId = countryId;
    }

    protected override async Task AnimateForTargetFaction()
    {
        CountryScene countryScene = CountryState.ForId(_countryId).CountryScene;
        UnitScene unitScene = UnitState.ForId(_unitId).UnitScene;

        if (unitScene == null) return;

        Tween tween = unitScene.CreateTween();
        tween.TweenProperty(unitScene.UnitSpriteNode, "scale", new Vector2(1.2f, 1.2f), GameSettings.DurationShortSeconds);
        tween.TweenProperty(unitScene.UnitSpriteNode, "scale", new Vector2(0f, 0f), GameSettings.DurationShortSeconds);
        await unitScene.ToSignal(tween, Tween.SignalName.Finished);
        tween.Dispose();

        // RemoveUnit parks the scene at the off-board position and hides it there (ResetToPoolState),
        // which also undoes what the tween above did to the sprite — so nothing to restore here.
        countryScene.RemoveUnit(unitScene);
    }
}
