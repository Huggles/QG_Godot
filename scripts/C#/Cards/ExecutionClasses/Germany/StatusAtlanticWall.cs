using Godot;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;

public partial class StatusAtlanticWall : StatusCardLogic
{
    protected override List<Condition> CardTriggers()
    {        
        return new List<Condition> {
            Condition.Build(
                new Condition.FactionBattled(StaticGameData.OpponentFactionTeamForFaction(Faction))
                .WithCountries(new List<int> { (int)Country.WesternEurope }), this)
        };
    }

    public override List<CardStep> InitializeReactCardSteps()
    {
        return new List<CardStep>
        {
            new CardStep(this, async() => {
                DiscardCardsChangeEvent discardCardsChangeEvent = BuildChangeEvent(new DiscardCardsChangeEvent(Faction, Faction.ITALY, 4));
                discardCardsChangeEvent.IsTrigger = true;
                await Task.CompletedTask;
                return discardCardsChangeEvent;  
            })
        };
    }
}
