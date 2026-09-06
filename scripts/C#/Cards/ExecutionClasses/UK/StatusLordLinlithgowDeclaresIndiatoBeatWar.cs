using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class StatusLordLinlithgowDeclaresIndiatoBeatWar : StatusCardLogic, ICountryTagModifier
{
    private static readonly List<Country> unlockedCountries = [Country.India];

    /// <summary>The space this card opens up for Army builds. It grants no build of its own — it
    /// re-tags India so any Army you may build may go there instead.</summary>
    public override TargetSet Targets() => TargetSet.Countries(unlockedCountries);

    public void ApplyTagModifiers(Faction faction)
    {
        CountryState india = CountryState.ForEnum(unlockedCountries[0]);
        if (india.CanRecruit(faction))
            india.AddTag(Tag.Buildable, faction);
    }
}