using Godot;
using System.Threading.Tasks;

/// <summary>A battle in an empty space: a shell hit and dust shockwave on land, a splash at sea.</summary>
public class BattleCountryAnimation : ChangeEventAnimation
{
    private readonly int _countryId;

    public BattleCountryAnimation(int countryId) => _countryId = countryId;

    protected override async Task AnimateForTargetFaction()
    {
        CountryState country = CountryState.ForId(_countryId);
        CountryScene scene = country?.CountryScene;
        if (scene == null || BattleFx.Pace <= 0f) return;

        Vector2 at = scene.GlobalCenter;
        if (country.IsSea)
        {
            BattleFx.Splash(at, 1.2f);
            await BattleFx.Wait(scene, 0.2);
            BattleFx.Splash(at + new Vector2(60, 25), 0.9f);
        }
        else
        {
            BattleFx.Impact(at, 1.1f);
            BattleFx.DustRing(at, 1.2f);
        }
        await BattleFx.Wait(scene, 0.6);
    }
}
