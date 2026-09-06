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

            new CardStep(this, async() => {
                
                var resp = await new InputRequest.SelectBattleTargetRequestHandler(Faction, CountryState.ForEnum(Country.SouthChinaSea).BattleTargets(Faction)).BroadCast();
                BattleTarget target = resp.ResponseCountryIds.Count > 0
                    ? new BattleTarget(resp.ResponseCountryIds[0], TargetType.COUNTRY)
                    : new BattleTarget(resp.ResponseUnitIds[0], TargetType.UNIT);
                BattleCountryChangeEvent battleCountryChangeEvent = BuildChangeEvent(target.ToAttackChangeEvent(Faction));
                battleCountryChangeEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(battleCountryChangeEvent);
            })
            .WithGuidance("Battle in the South China Sea")
            .WithConditions( () => { return new List<Condition> { new Condition.CountryIsAttackable([CountryState.ForEnum(Country.SouthChinaSea).Id], Faction) }; } ),
            new CardStep(this, async() => {
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, new List<int>{ CountryState.ForEnum(Country.SouthEastAsia).Id }).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.RECRUIT));
                deployUnitChangeEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(deployUnitChangeEvent);
            }).WithConditions( ()=>{ return new List<Condition>{new Condition.CountryIsRecruitable([CountryState.ForEnum(Country.SouthEastAsia).Id], Faction)}; } )
            .WithGuidance("Recruit an army in South East Asia")

        };
    }
}
