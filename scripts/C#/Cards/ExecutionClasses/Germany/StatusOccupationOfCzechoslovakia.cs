using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class StatusOccupationOfCzechoslovakia : StatusCardLogic, IStatusVictoryPoints
{
    public virtual VPEntry AddVictoryPoints()
    {
        bool factionAttacked = CardPlayPool.GetChangeEvents<BattleCountryChangeEvent>().Where(ce => ce.TriggeringFaction == Faction).ToList().Count > 0;
        int score = factionAttacked ? 1 : 0;
        return new VPEntry(score, $"{score} victory points for not conducting a battle or eliminating a unit");
    }

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> { Condition.Build(new Condition.IsVictoryPointStep(), this) };
    }
}