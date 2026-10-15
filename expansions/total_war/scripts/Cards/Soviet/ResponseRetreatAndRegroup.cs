using System.Collections.Generic;

public partial class ResponseRetreatAndRegroup : ResponseCardLogic
{
    private static readonly List<int> fallbackFrom = [(int)Country.Moscow, (int)Country.Ukraine];
    private static readonly List<Country> siberia = [Country.Siberia];
    private static readonly List<Country> kazakhstan = [Country.Kazakhstan];

    public override TargetSet Targets() =>
        TargetSet.Countries(TotalWarCards.DeployTargets(BoardState.Live, Faction, DeployType.RECRUIT, UnitType.ARMY, [Country.Siberia, Country.Kazakhstan]));

    protected override List<Condition> CardTriggers() => new()
    {
        Condition.Build(new Condition.CustomCondition(s =>
            s.ReactionTrigger is RemoveUnitChangeEvent removed
            && removed.UnitState.Faction == Faction
            && removed.UnitState.IsArmy
            && fallbackFrom.Contains(removed.CountryId)).InReactionWindow(), this)
    };

    public override List<CardStep> OnActivate() => new()
    {
        TotalWarCards.DeployStep(this, DeployType.RECRUIT, UnitType.ARMY, siberia, "Recruit an Army in Siberia"),
        TotalWarCards.DeployStep(this, DeployType.RECRUIT, UnitType.ARMY, kazakhstan, "Recruit an Army in Kazakhstan")
    };
}
