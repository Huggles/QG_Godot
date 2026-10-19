using System.Collections.Generic;
using System.Linq;

/// <summary>"Use at the beginning of your Victory step. Discard the top 1 card of your draw deck to deploy an Air Force in or adjacent to the Central Pacific."</summary>
public partial class BolsterPappyBoyington : BolsterCardLogic
{
    private static List<Country> CentralPacificArea => CountryState.ForEnum(Country.CentralPacific).ConnectedCountryIds
        .Select(countryId => CountryState.ForId(countryId).Country).Append(Country.CentralPacific).ToList();

    private bool CanDeploy(BoardState board) => TotalWarCards.AirDeployTargets(board, Faction, CentralPacificArea).Count > 0;

    public override TargetSet Targets() => TargetSet.Countries(TotalWarCards.AirDeployTargets(BoardState.Live, Faction, CentralPacificArea));

    protected override List<Condition> CardTriggers() => TotalWarCards.AtStepStart(this, TurnStep.VICTORY_POINT, s => CanDeploy(s.Board));

    public override List<CardStep> OnActivate() => new()
    {
        TotalWarCards.DeckDiscardStep(this, 1, "Discard the top card of your draw deck to deploy an Air Force at the Central Pacific", s => CanDeploy(s.Board)),
        TotalWarCards.AirDeployStep(this, Faction, "Deploy an Air Force in or next to the Central Pacific", CentralPacificArea).RequiringPreviousStep()
    };
}
