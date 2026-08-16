using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EWDecimaFlottigliaMASFrogmen : EWCardLogic
{
    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new CardStep(this, async() => {
                bool noAlliedNavyInMediterranean = !StaticGameData.FactionsForTeam(FactionTeam.ALLIES)
                    .SelectMany(faction => FactionState.ForEnum(faction).ActiveUnitIds.ToUnitStates())
                    .Any(us => us.Type == UnitType.NAVY
                               && us.CountryState.Country == Country.MediterraneanSea);

                int totalDiscards = 1 + (noAlliedNavyInMediterranean ? 1 : 0);

                await CardPlayPool.DoChangeEvent(new ScorePointsChangeEvent(new VPEntry(1, "Decima Flottiglia MAS Frogmen"), Faction));

                ForceDiscardCardsChangeEvent discardEvent = BuildChangeEvent(new ForceDiscardCardsChangeEvent(Faction, Faction.UNITED_KINGDOM, totalDiscards));
                discardEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(discardEvent);
            })
        };
    }
}