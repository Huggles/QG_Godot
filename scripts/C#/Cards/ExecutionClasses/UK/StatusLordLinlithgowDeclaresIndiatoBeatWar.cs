using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class StatusLordLinlithgowDeclaresIndiatoBeatWar : StatusCardLogic, ICountryTagModifier
{
    public void ApplyTagModifiers(Faction faction)
    {
        CountryState india = CountryState.ForEnum(Country.India);
        if (india.CanRecruit(faction))
            india.AddTag(Tag.Buildable, faction);
    }
}