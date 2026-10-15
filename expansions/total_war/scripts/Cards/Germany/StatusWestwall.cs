using System.Collections.Generic;

public partial class StatusWestwall : StatusCardLogic
{
    private static readonly List<int> germany = [(int)Country.Germany];

    /// <summary>Germany, the space this guards, plus the battle that woke it. As StatusAtlanticWall.</summary>
    public override TargetSet Targets() => TargetSet.Countries(germany).Plus(TriggerTargets());

    /// <summary>
    /// "Every time" (MultipleActivationsPerTurn): an immediate trigger, so it re-arms after each use and
    /// answers each battle once. Any Axis Army in Germany counts, not only a German one.
    /// </summary>
    protected override List<Condition> CardTriggers() => new()
    {
        Condition.Build(new Condition.FactionBattled(StaticGameData.OpponentFactionTeamForFaction(Faction))
            .Immediately().WithCountries(germany).WithTargetFactionTeam(FactionTeam.AXIS), this),
        Condition.Build(new Condition.CustomCondition(s => s.ReactionTrigger is BattleUnitChangeEvent { UnitState.IsArmy: true }), this)
    };

    /// <summary>The attacker discards; a French or Chinese attacker's patron pays, as every forced discard does.</summary>
    public override List<CardStep> OnActivate() => new()
    {
        new ResultStep(this, Choose.Fixed(c =>
                new ForceDiscardCardsChangeEvent(Faction, TriggerContextAs<BattleCountryChangeEvent>(c.Situation).TriggeringFaction, 3)))
            .WithGuidance("Make the attacker discard the top 3 cards of its draw deck")
    };
}
