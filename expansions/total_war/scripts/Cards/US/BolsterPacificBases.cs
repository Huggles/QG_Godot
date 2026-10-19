using System.Collections.Generic;
using System.Linq;

/// <summary>"Use at the beginning of your Victory step. Discard the top 1 card of your draw deck to recruit an Army in a space adjacent to the Central Pacific."</summary>
public partial class BolsterPacificBases : BolsterCardLogic
{
    private static List<Country> NextToCentralPacific => CountryState.ForEnum(Country.CentralPacific).ConnectedCountryIds
        .Select(countryId => CountryState.ForId(countryId).Country).ToList();

    private bool CanRecruit(BoardState board) =>
        TotalWarCards.DeployTargets(board, Faction, DeployType.RECRUIT, UnitType.ARMY, NextToCentralPacific).Count > 0;

    public override TargetSet Targets() =>
        TargetSet.Countries(TotalWarCards.DeployTargets(BoardState.Live, Faction, DeployType.RECRUIT, UnitType.ARMY, NextToCentralPacific));

    protected override List<Condition> CardTriggers() => TotalWarCards.AtStepStart(this, TurnStep.VICTORY_POINT, s => CanRecruit(s.Board));

    public override List<CardStep> OnActivate() => new()
    {
        TotalWarCards.DeckDiscardStep(this, 1, "Discard the top card of your draw deck to recruit an Army next to the Central Pacific", s => CanRecruit(s.Board)),
        TotalWarCards.DeployStep(this, DeployType.RECRUIT, UnitType.ARMY, NextToCentralPacific, "Recruit an Army next to the Central Pacific").RequiringPreviousStep()
    };
}
