using System.Collections.Generic;
using System.Linq;

/// <summary>
/// "Use at the beginning of your Play step. Discard 1 card from your hand to deploy or marshal an Air Force in
/// a space with your Navy." Italy has one Air Force: marshalled when it is on the board, else deployed.
/// </summary>
public partial class BolsterItalyCompletesCarriers : BolsterCardLogic
{
    private int AirForceOnBoard(BoardState board) => board.ActiveAirForceIds(Faction).FirstOrDefault(-1);

    /// <summary>The spaces with an Italian Navy the Air Force may go to: by marshal if it is on the board, by deploy if not.</summary>
    private List<int> Targets(BoardState board)
    {
        List<int> navySpaces = board.ActiveUnits(Faction).Where(unit => unit.IsNavy).Select(unit => board.CountryOf(unit)).Distinct().ToList();
        if (AirForceOnBoard(board) is var airForce && airForce >= 0)
            return board.MarshalTargets(UnitState.ForId(airForce)).Where(navySpaces.Contains).ToList();
        return board.AvailableUnitCount(Faction, UnitType.AIR_FORCE) == 0
            ? new()
            : navySpaces.Where(countryId => board.CanDeployAirForce(Faction, CountryState.ForId(countryId))).ToList();
    }

    public override TargetSet Targets() => TargetSet.Countries(Targets(BoardState.Live));

    protected override List<Condition> CardTriggers() => TotalWarCards.AtPlayStepStart(this)
        .Append(Condition.Build(new Condition.CustomCondition(s => Targets(s.Board).Count > 0), this))
        .ToList();

    public override List<CardStep> OnActivate() => new()
    {
        TotalWarCards.HandDiscardStep(this, 1, "Discard 1 card to deploy or marshal an Air Force to a space with your Navy",
            s => Targets(s.Board).Count > 0),
        new ResultStep(this, Choose.CountryFrom(c => Targets(c.Board), (countryId, c) => AirForceOnBoard(c.Board) is var airForce && airForce >= 0
                ? new MoveAirForceChangeEvent(Faction, airForce, countryId, AirForceMoveReason.MARSHAL, c.Board)
                : new DeployAirForceChangeEvent(Faction, countryId)))
            .RequiringPreviousStep()
            .WithGuidance("Deploy or marshal an Air Force to a space with your Navy")
    };
}
