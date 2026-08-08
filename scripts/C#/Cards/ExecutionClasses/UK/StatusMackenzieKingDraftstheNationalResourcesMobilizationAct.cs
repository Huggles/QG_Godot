using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class StatusMackenzieKingDraftstheNationalResourcesMobilizationAct : StatusCardLogic, IVPModifier
{
    public virtual VPEntry AddVictoryPoints()
    {
        int score = 0;
        if (FactionState.ForEnum(Faction).ActiveUnitIds.ToUnitStates().Any(u => u.CountryState.Country == Country.NorthAtlantic && u.Type == UnitType.NAVY)) score++;
        if (FactionState.ForEnum(Faction).ActiveUnitIds.ToUnitStates().Any(u => u.CountryState.Country == Country.Canada && u.Type == UnitType.ARMY)) score++;
        return new VPEntry(score, $"a navy in {CountryState.ForEnum(Country.NorthAtlantic).Label} and army in {CountryState.ForEnum(Country.Canada).Label}");
    }

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> { Condition.Build(new Condition.IsVictoryPointStep(), this) };
    }
}