using System.Collections.Generic;

public partial class StatusAntiJapaneseVolunteerArmies : StatusCardLogic
{
    /// <summary>A Chinese Army battled, built or recruited. An EventCondition, so the trigger is immediate and the card re-arms per event.</summary>
    private sealed class ChineseArmyStirs : Condition.EventCondition
    {
        public override bool IsMatch(ChangeEvent ce) => ce switch
        {
            BattleUnitChangeEvent battle => battle.IsBattle && battle.UnitState.Faction == Faction.CHINA && battle.UnitState.IsArmy,
            DeployUnitChangeEvent deploy => deploy.TriggeringFaction == Faction.CHINA && deploy.UnitType == UnitType.ARMY
                                            && deploy.DeploymentType is DeployType.BUILD or DeployType.RECRUIT,
            _ => false
        };
    }

    /// <summary>The Chinese Army battled or deployed, from the window's event.</summary>
    public override TargetSet Targets() => TriggerTargets();

    protected override List<Condition> CardTriggers() => new()
    {
        Condition.Build(new ChineseArmyStirs().Immediately(), this),
        Condition.Build(new Condition.CustomCondition(s => s.Board.ForFaction(Faction.JAPAN).Hand.Count > 0), this)
    };

    public override List<CardStep> OnActivate() => new()
    {
        new ResultStep(this, Choose.Fixed(_ => new ForceDiscardHandCardsChangeEvent(Faction, Faction.JAPAN, 1)))
            .WithGuidance("Make Japan discard a card from its hand")
    };
}
