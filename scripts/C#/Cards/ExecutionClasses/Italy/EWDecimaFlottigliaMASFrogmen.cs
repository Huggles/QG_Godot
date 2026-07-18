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
                bool noAlliedNavyInMediterranean = !GameSession.Current.GameState.UnitStatesById.Values
                    .Any(us => StaticGameData.FactionTeamForFaction(us.Faction) == FactionTeam.ALLIES
                               && us.Type == UnitType.NAVY
                               && us.CountryState.Country == Country.MediterraneanSea);

                int totalDiscards = 1 + (noAlliedNavyInMediterranean ? 1 : 0);

                await CardPlayPool.DoChangeEvent(new ScorePointsChangeEvent(new VPEntry(1, "1 VP for Decima Flottiglia MAS Frogmen."), Faction));

                ForceDiscardCardsChangeEvent discardEvent = BuildChangeEvent(new ForceDiscardCardsChangeEvent(Faction, Faction.UNITED_KINGDOM, totalDiscards));
                discardEvent.IsTrigger = true;
                return discardEvent;
            })
        };
    }
}