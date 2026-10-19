using System.Collections.Generic;

/// <summary>
/// "Use when you play an Economic Warfare card with the word 'Submarines' in the title. Discard the top 1
/// card of your draw deck to battle a sea space." Offered in the windows that EW card's own steps open.
/// </summary>
public partial class BolsterG7eTorpedo : BolsterCardLogic
{
    private bool SubmarinesPlayed(GameSituation situation) =>
        TotalWarCards.EconomicWarfarePlayed(situation, Faction) is { } economicWarfare && economicWarfare.CardData.Label.Contains("Submarines");

    private List<BattleTarget> BattleTargets(GameSituation situation) =>
        TotalWarCards.BattleTargetsOn(situation.Board, Faction, CountryType.SEA);

    public override TargetSet Targets() => TargetSet.FromBattleTargets(BattleTargets(GameSituation.Live));

    protected override List<Condition> CardTriggers() => new()
    {
        Condition.Build(new Condition.FactionPlayedCard(Faction), this),
        Condition.Build(new Condition.IsGameFlowStep(TurnStep.PLAY_CARD), this),
        Condition.Build(new Condition.IsFactionTurn(Faction), this),
        Condition.Build(new Condition.CustomCondition(s => SubmarinesPlayed(s) && BattleTargets(s).Count > 0), this)
    };

    public override List<CardStep> OnActivate() => new()
    {
        TotalWarCards.DeckDiscardStep(this, 1, "Discard the top card of your draw deck to battle a sea space", s => BattleTargets(s).Count > 0),
        TotalWarCards.BattleStep(this, BattleTargets, "Battle a sea space").RequiringPreviousStep()
    };
}
