using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class StatusAustraliaFormstheDirectorateofManpower : StatusCardLogic, ICountryTagModifier
{
    public void ApplyTagModifiers(Faction faction)
    {
        CountryState australia = CountryState.ForEnum(Country.Australia);
        if (australia.CanRecruit(faction))
            australia.AddTag(Tag.Buildable, faction);
    }
}