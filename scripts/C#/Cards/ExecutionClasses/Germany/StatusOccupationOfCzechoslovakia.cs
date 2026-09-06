using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class StatusOccupationOfCzechoslovakia : StatusCardLogic, IVPModifier
{
    // No Targets() override: the point is earned by what you did NOT do this turn (no land battle,
    // no unit eliminated). A condition on turn history names no country and no unit.

    public virtual VPEntry AddVictoryPoints()
    {
        bool factionAttacked = CardPlayPool.GetChangeEvents<BattleCountryChangeEvent>().Any(ce => ce.IsBattle && ce.TriggeringFaction == Faction);
        bool factionEliminated = CardPlayPool.GetChangeEvents<RemoveUnitChangeEvent>().Any(ce => ce.TriggeringFaction == Faction);
        int score = (factionAttacked || factionEliminated) ? 0 : 1;
        return new VPEntry(score, "not conducting a battle or eliminating a unit");
    }

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> { Condition.Build(new Condition.IsVictoryPointStep(), this) };
    }
}