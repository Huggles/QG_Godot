using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class StatusAtlanticWall : StatusCardLogic
{
    private static readonly List<int> guardedCountryIds = [(int)Country.WesternEurope];

    /// <summary>
    /// Western Europe, the space this guards — plus, in a reaction window, the battle that woke it.
    /// The card selects nothing itself: the penalty falls on the attacker's deck, which has no place
    /// on the board, so what is worth showing is where the card watches.
    /// </summary>
    public override TargetSet Targets() =>
        TargetSet.Countries(guardedCountryIds).Plus(TriggerTargets());

    protected override List<Condition> CardTriggers()
    {        
        return new List<Condition> {
            Condition.Build(
                new Condition.FactionBattled(StaticGameData.OpponentFactionTeamForFaction(Faction))
                .Immediately().WithCountries(guardedCountryIds), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new CardStep(this, async() => {
                Faction attackingFaction = TriggerContextAs<BattleCountryChangeEvent>().TriggeringFaction;
                ForceDiscardCardsChangeEvent ForceDiscardCardsChangeEvent = BuildChangeEvent(new ForceDiscardCardsChangeEvent(Faction, attackingFaction, 3));
                ForceDiscardCardsChangeEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(ForceDiscardCardsChangeEvent);
            })
            .WithGuidance("The attacker discards 3 cards")
        };
    }
}
