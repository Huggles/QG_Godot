using System.Collections.Generic;
using System.Linq;

public partial class ResponseMarcoPoloBridgeIncident : ResponseCardLogic
{
    public override bool IsFreePlayStepActivation => true;

    /// <summary>China and the land spaces next to it. Read when needed, not at class load, which runs before the map is.</summary>
    private static List<Country> ChinaAndNeighbours =>
        CountryState.ForEnum(Country.China).ConnectedCountryStates.Where(country => country.IsLand)
            .Select(country => country.Country).Append(Country.China).ToList();

    private List<BattleTarget> BattleTargets(GameSituation situation) =>
        TotalWarCards.BattleTargetsIn(situation.Board, Faction, ChinaAndNeighbours);

    public override TargetSet Targets() => TargetSet.FromBattleTargets(BattleTargets(GameSituation.Live));

    protected override List<Condition> CardTriggers() => TotalWarCards.AtPlayStepStart(this);

    public override List<CardStep> OnActivate() => new()
    {
        TotalWarCards.BattleStep(this, BattleTargets, "Battle in China or an adjacent land space")
    };
}
