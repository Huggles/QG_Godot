using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class StatusAustraliaFormstheDirectorateofManpower : StatusCardLogic, ICountryTagModifier
{
    private static readonly List<Country> unlockedCountries = [Country.Australia];

    /// <summary>The space this card opens up for Army builds. It grants no build of its own — it
    /// re-tags Australia so any Army you may build may go there instead.</summary>
    public override TargetSet Targets() => TargetSet.Countries(unlockedCountries);

    public void ApplyTagModifiers(Faction faction)
    {
        CountryState australia = CountryState.ForEnum(unlockedCountries[0]);
        if (australia.CanRecruit(faction))
            australia.AddTag(Tag.Buildable, faction);
    }
}