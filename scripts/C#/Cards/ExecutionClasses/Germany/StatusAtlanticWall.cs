using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
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

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new CardStep(this, async() => {
                Faction attackingFaction = CardPlayPool.GetChangeEvents<BattleCountryChangeEvent>()
                    .Last(ce => ce.CountryState.Country == Country.WesternEurope).TriggeringFaction;
                ForceDiscardCardsChangeEvent ForceDiscardCardsChangeEvent = BuildChangeEvent(new ForceDiscardCardsChangeEvent(Faction, attackingFaction, 3));
                ForceDiscardCardsChangeEvent.IsTrigger = true;
                await Task.CompletedTask;
                return ForceDiscardCardsChangeEvent;  
            })
            .WithGuidance("The attacker discards 3 cards")
        };
    }
}
