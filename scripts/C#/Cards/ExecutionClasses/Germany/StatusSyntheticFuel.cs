using System.Threading.Tasks;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class StatusSyntheticFuel : StatusCardLogic
{    
    protected override List<Condition> CardTriggers()
    {        
        return new List<Condition> {
            Condition.Build(new Condition.FactionDeployed(Faction, DeployType.BUILD), this).Immediately(),
            // Built per situation: which spaces are adjacent depends on the deploy being reacted to.
            Condition.Build(
                new Condition.CustomCondition(s =>
                    new Condition.CountryIsBuildable(
                        DeployTargets(s).ToCountryIds(),
                        Faction).MeetCondition(s)),
                this
            )

        };
    }

    public List<CountryState> DeployTargets(GameSituation situation)
    {
        var trigger = TriggerContextAs<DeployUnitChangeEvent>(situation);
        if (trigger == null) return new List<CountryState>();
        return situation.Board.AdjacentCountryStates(Faction, CountryState.ForId(trigger.CountryId))
            .Where(countryState => situation.Board.CanBuild(Faction, countryState) && countryState.IsLand)
            .Distinct()
            .ToList();
    }

    /// <summary>The spaces adjacent to the deploy that can take the second Army.</summary>
    public override TargetSet Targets() => TargetSet.Countries(DeployTargets(GameSituation.Live));

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new RequirementStep(this, Choose.Fixed(_ => new ForceDiscardCardsChangeEvent(Faction, Faction, 2)))
            .WithGuidance("Deploy an army adjacent to where it deployed an army this turn"),

            new ResultStep(this, Choose.CountryFrom(c => DeployTargets(c.Situation).ToCountryIds(),
                (countryId, _) => new DeployUnitChangeEvent(Faction, countryId, DeployType.BUILD)))
            .RequiringPreviousStep()
        };
    }
}
