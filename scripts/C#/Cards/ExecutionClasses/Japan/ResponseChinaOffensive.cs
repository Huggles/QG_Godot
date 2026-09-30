using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class ResponseChinaOffensive : ResponseCardLogic
{
    private List<CountryState> TargetCountries => CountryState.ForEnums(new List<Country>
    {
        Country.China,
        Country.Vladivostok,
        Country.Mongolia,
        Country.Szechuan,
        Country.SouthEastAsia,
    });

    private List<BattleTarget> BattleTargets(BoardState board) => BattleTarget.In(board, TargetCountries.ToCountryIds(), Faction);

    /// <summary>The space just battled, where step 1 builds, plus what step 2 may attack.</summary>
    public override TargetSet Targets() =>
        TargetSet.Countries(EligibleAttackedCountries(GameSituation.Live))
            .Plus(TargetSet.FromBattleTargets(BattleTargets(BoardState.Live)));

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> { Condition.Build(new Condition.FactionBattled(Faction).Immediately().WithCountries(TargetCountries.ToCountryIds()), this) };
    }
 
    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new ResultStep(this, Choose.CountryFrom(c => EligibleAttackedCountries(c.Situation),
                (countryId, _) => new DeployUnitChangeEvent(Faction, countryId, DeployType.BUILD)))
            // One CountryIsEmpty per eligible country, all required: none eligible passes, as the empty list did.
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => EligibleAttackedCountries(s)
                .All(countryId => Condition.Build(new Condition.CountryIsEmpty(countryId), this).MeetCondition(s))), this))
            .WithGuidance("Build an army in the country just battled"), 

            new ResultStep(this, Choose.BattleTargetFrom(c => BattleTargets(c.Board),
                (target, c) => target.ToAttackChangeEvent(Faction, c.Board)))
            .WithCondition(
                ()=>{ return Condition.Build(new Condition.CountryIsAttackable(TargetCountries.ToCountryIds(), Faction), this); }
            ).WithGuidance("Battle in China or an adjacent land space"),
        };
    }

    private List<int> EligibleAttackedCountries(GameSituation situation)
    {
        if (TriggerContextIn(situation) is BattleCountryChangeEvent trigger
            && trigger.TriggeringFaction == Faction
            && TargetCountries.Contains(trigger.CountryState))
        {
            return new List<int> { trigger.CountryId };
        }
        return new List<int>();
    }
}
