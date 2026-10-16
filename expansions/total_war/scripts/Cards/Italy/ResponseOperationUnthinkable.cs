using System.Collections.Generic;
using System.Linq;

public partial class ResponseOperationUnthinkable : ResponseCardLogic
{
    private static readonly List<Faction> westernAllies = [Faction.UNITED_KINGDOM, Faction.UNITED_STATES];

    /// <summary>The Soviet Army just built, when a UK or US Army stands next to it (as the Axis sees adjacency); else null.</summary>
    private DeployUnitChangeEvent Target(GameSituation situation, DeployUnitChangeEvent built)
    {
        if (built is not { TriggeringFaction: Faction.SOVIET, DeploymentType: DeployType.BUILD, UnitType: UnitType.ARMY }) return null;
        List<int> neighbours = situation.Board.AdjacentCountryIds(Faction, built.CountryState);
        bool watched = westernAllies.Any(ally => situation.Board.ActiveUnits(ally).Any(unit => unit.IsArmy && neighbours.Contains(situation.Board.CountryOf(unit))));
        return watched ? built : null;
    }

    public override TargetSet Targets() => TriggerTargets();

    protected override List<Condition> CardTriggers() => new()
    {
        Condition.Build(new Condition.CustomCondition(s => Target(s, s.ReactionTrigger as DeployUnitChangeEvent) != null).InReactionWindow(), this)
    };

    public override List<CardStep> OnActivate() => new()
    {
        new ResultStep(this, Choose.Fixed(c =>
                new RemoveUnitChangeEvent(Faction, TriggerContextAs<DeployUnitChangeEvent>(c.Situation).UnitId, UnitRemovalReason.ELIMINATE, c.Board)))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => Target(s, TriggerContextAs<DeployUnitChangeEvent>(s)) != null), this))
            .WithGuidance("Eliminate the Soviet Army just built")
    };
}
