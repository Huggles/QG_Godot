using Godot;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Plays the scale-pop tween on a unit that was just deployed to a country.
/// The unit is already positioned in the CountryScene by the time this runs.
/// </summary>
public class DeployUnitAnimation : ChangeEventAnimation
{
    private readonly int _unitId;
    private readonly int _countryId;
    
    public DeployUnitAnimation(int unitId, int countryId)
    {
        _unitId = unitId;
        _countryId = countryId;
    }

    public override async Task Execute()
    {
        CountryScene countryScene = CountryState.ForId(_countryId).CountryScene;
        UnitScene unitScene = countryScene.AddUnit(_unitId);
        
        Tween tween = unitScene.CreateTween();
        tween.TweenProperty(unitScene.UnitSpriteNode, "scale", new Vector2(UnitScene.DefaultSpriteScale*1.5f, UnitScene.DefaultSpriteScale*1.5f), GameSettings.AnimationDurationSeconds / 2);
        tween.TweenProperty(unitScene.UnitSpriteNode, "scale", new Vector2(UnitScene.DefaultSpriteScale, UnitScene.DefaultSpriteScale), GameSettings.AnimationDurationSeconds / 2);
        await unitScene.ToSignal(tween, Tween.SignalName.Finished);
        tween.Dispose();
    }
}
