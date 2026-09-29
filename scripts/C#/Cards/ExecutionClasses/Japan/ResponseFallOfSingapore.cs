using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class ResponseFallOfSingapore : ResponseCardLogic
{
    private CountryState SouthEastAsia => CountryState.ForEnum(Country.SouthEastAsia);
    private CountryState SouthChinaSea => CountryState.ForEnum(Country.SouthChinaSea);

    /// <summary>What is attackable in the South China Sea, plus the space the recruit lands in.</summary>
    public override TargetSet Targets() =>
        TargetSet.FromBattleTargets(SouthChinaSea.BattleTargets(Faction))
            .Plus(TargetSet.Countries(new[] { SouthEastAsia }));

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> { Condition.Build(new Condition.FactionBattled(Faction).Immediately().WithCountries([SouthEastAsia.Id]), this) };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {

            new ResultStep(this, Choose.BattleTargetFrom(c => c.Board.BattleTargets(Faction, CountryState.ForEnum(Country.SouthChinaSea)),
                (target, _) => target.ToAttackChangeEvent(Faction)))
            .WithGuidance("Battle in the South China Sea")
            .WithConditions( () => { return new List<Condition> { new Condition.CountryIsAttackable([CountryState.ForEnum(Country.SouthChinaSea).Id], Faction) }; } ),
            new ResultStep(this, Choose.CountryFrom(_ => new List<int>{ CountryState.ForEnum(Country.SouthEastAsia).Id },
                (countryId, _) => new DeployUnitChangeEvent(Faction, countryId, DeployType.RECRUIT)))
            .WithConditions( ()=>{ return new List<Condition>{new Condition.CountryIsRecruitable([CountryState.ForEnum(Country.SouthEastAsia).Id], Faction)}; } )
            .WithGuidance("Recruit an army in South East Asia")

        };
    }
}
