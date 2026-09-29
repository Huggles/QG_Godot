using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class ResponseDefenseoftheMotherland : ResponseCardLogic
{
    private List<int> MoscowAndAdjacentIds
    {
        get
        {
            var moscowCs = CountryState.ForEnum(Country.Moscow);
            return new List<CountryState> { moscowCs }
                .Concat(moscowCs.ConnectedCountryStates)
                .Select(cs => cs.Id)
                .ToList();
        }
    }

    private List<int> RecruitableNearMoscowIds(BoardState board) =>
        MoscowAndAdjacentIds
            .Where(id => board.Of(CountryState.ForId(id)).Tags.Has(Tag.Recruitable, Faction))
            .ToList();

    private List<int> AxisArmiesInMoscow(BoardState board) =>
        board.UnitsIn(CountryState.ForEnum(Country.Moscow)).Values
            .Where(uId => {
                var u = UnitState.ForId(uId);
                return StaticGameData.FactionTeamForFaction(u.Faction) == FactionTeam.AXIS && u.IsArmy && !board.ImmuneForTurn(u);
            })
            .ToList();

    /// <summary> A turn-start recruit and elimination, not a substitute for the hand card. </summary>
    public override bool IsFreePlayStepActivation => true;

    /// <summary>Where the new Army may go, and the Axis Armies in Moscow it may then clear out.</summary>
    public override TargetSet Targets() =>
        TargetSet.Countries(RecruitableNearMoscowIds(BoardState.Live)).Plus(TargetSet.Units(AxisArmiesInMoscow(BoardState.Live)));

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            // "At the beginning of your turn" = the Play step with the play still unspent. See
            // StatusVolksturm for why this is not TurnStep.START. Activating it does not spend
            // the play.
            Condition.Build(new Condition.IsPlayCardStep(), this),
            Condition.Build(new Condition.IsFactionTurn(Faction), this),
            Condition.Build(new Condition.CountryIsRecruitable(MoscowAndAdjacentIds, Faction), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new ResultStep(this, Choose.CountryFrom(c => RecruitableNearMoscowIds(c.Board),
                (countryId, _) => new DeployUnitChangeEvent(Faction, countryId, DeployType.RECRUIT)))
            .WithCondition(() => Condition.Build(new Condition.CountryIsRecruitable(MoscowAndAdjacentIds, Faction), this))
            .WithGuidance("Recruit an Army in or adjacent to Moscow"),

            new ResultStep(this, Choose.UnitFrom(c => AxisArmiesInMoscow(c.Board),
                (unitId, c) => new RemoveUnitChangeEvent(Faction, unitId, UnitRemovalReason.ELIMINATE, c.Board)))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => AxisArmiesInMoscow(s.Board).Count > 0), this))
            .WithGuidance("Eliminate an Axis Army in Moscow")
        };
    }
}