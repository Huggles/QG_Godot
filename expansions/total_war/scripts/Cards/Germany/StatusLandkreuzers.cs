using System.Collections.Generic;

public partial class StatusLandkreuzers : StatusCardLogic
{
    /// <summary>The German Army being battled, from the window's battle.</summary>
    public override TargetSet Targets() => TriggerTargets();

    /// <summary>
    /// "Every time" (MultipleActivationsPerTurn): an immediate trigger, so it re-arms after each use and
    /// answers each battle once. The second condition keeps out a battled German Navy.
    /// </summary>
    protected override List<Condition> CardTriggers() => new()
    {
        Condition.Build(new Condition.FactionBattled(StaticGameData.OpponentFactionTeamForFaction(Faction))
            .Immediately().WithTargetFaction(Faction.GERMANY), this),
        Condition.Build(new Condition.CustomCondition(s => s.ReactionTrigger is BattleUnitChangeEvent { UnitState.IsArmy: true }), this)
    };

    /// <summary>The attacker discards; a French or Chinese attacker's patron pays, as every forced discard does.</summary>
    public override List<CardStep> OnActivate() => new()
    {
        new ResultStep(this, Choose.Fixed(c =>
                new ForceDiscardCardsChangeEvent(Faction, TriggerContextAs<BattleCountryChangeEvent>(c.Situation).TriggeringFaction, 2)))
            .WithGuidance("Make the attacker discard the top 2 cards of its draw deck")
    };
}
