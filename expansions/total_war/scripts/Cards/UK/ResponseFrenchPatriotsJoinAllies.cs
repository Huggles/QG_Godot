using System.Collections.Generic;

public partial class ResponseFrenchPatriotsJoinAllies : ResponseCardLogic
{
    private static readonly List<Faction> battlers = [Faction.UNITED_STATES, Faction.FRANCE, Faction.UNITED_KINGDOM];
    private static readonly List<Country> battlegrounds = [Country.NorthAfrica, Country.Africa, Country.MiddleEast, Country.WesternEurope];

    /// <summary>The space just battled, when the battle is one this card answers; else null.</summary>
    private CountryState Battled(GameSituation situation) =>
        TriggerContextAs<BattleCountryChangeEvent>(situation) is { IsBattle: true } battle
        && battlers.Contains(battle.TriggeringFaction)
        && battlegrounds.Contains(battle.CountryState.Country)
            ? battle.CountryState
            : null;

    private bool CanRecruit(GameSituation situation) =>
        Battled(situation) is { } space && situation.Board.Of(space).Tags.Has(Tag.Recruitable, Faction.FRANCE);

    public override TargetSet Targets() => TriggerTargets();

    protected override List<Condition> CardTriggers() => new()
    {
        Condition.Build(new Condition.CustomCondition(CanRecruit).InReactionWindow(), this)
    };

    public override List<CardStep> OnActivate() => new()
    {
        new ResultStep(this, Choose.Fixed(c => new DeployUnitChangeEvent(Faction.FRANCE, Battled(c.Situation).Id, DeployType.RECRUIT)))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(CanRecruit), this))
            .WithGuidance("Recruit a French Army in the space just battled")
    };
}
