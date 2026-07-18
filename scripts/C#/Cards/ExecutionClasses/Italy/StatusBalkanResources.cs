using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class StatusBalkanResources : StatusCardLogic, IVPModifier
{
    public virtual VPEntry AddVictoryPoints()
    {
        int score = FactionState.ForEnum(Faction).ActiveUnitIds.ToUnitStates()
            .Any(us => us.CountryState.Country == Country.Balkans && us.Type == UnitType.ARMY) ? 1 : 0;
        return new VPEntry(score, score == 1 ? "1 victory point for an Italian Army in the Balkans." : "No Italian Army in the Balkans.");
    }

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> { Condition.Build(new Condition.IsVictoryPointStep(), this) };
    }
}