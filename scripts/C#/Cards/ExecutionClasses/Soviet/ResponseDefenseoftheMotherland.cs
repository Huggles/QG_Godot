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

    private List<int> RecruitableNearMoscowIds =>
        MoscowAndAdjacentIds
            .Where(id => CountryState.ForId(id).Tags.Has(Tag.Recruitable, Faction))
            .ToList();

    private List<int> AxisArmiesInMoscow =>
        CountryState.ForEnum(Country.Moscow).Units.Values
            .Where(uId => {
                var u = UnitState.ForId(uId);
                return StaticGameData.FactionTeamForFaction(u.Faction) == FactionTeam.AXIS && u.IsArmy && !u.ImmuneForTurn;
            })
            .ToList();

    /// <summary> A turn-start recruit and elimination, not a substitute for the hand card. </summary>
    public override bool IsFreePlayStepActivation => true;

    /// <summary>Where the new Army may go, and the Axis Armies in Moscow it may then clear out.</summary>
    public override TargetSet Targets() =>
        TargetSet.Countries(RecruitableNearMoscowIds).Plus(TargetSet.Units(AxisArmiesInMoscow));

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
            new ResultStep(this, Choose.CountryFrom(() => RecruitableNearMoscowIds,
                countryId => new DeployUnitChangeEvent(Faction, countryId, DeployType.RECRUIT)))
            .WithCondition(() => Condition.Build(new Condition.CountryIsRecruitable(MoscowAndAdjacentIds, Faction), this))
            .WithGuidance("Recruit an Army in or adjacent to Moscow"),

            new ResultStep(this, Choose.UnitFrom(() => AxisArmiesInMoscow,
                unitId => new RemoveUnitChangeEvent(Faction, unitId, UnitRemovalReason.ELIMINATE)))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(() => AxisArmiesInMoscow.Count > 0), this))
            .WithGuidance("Eliminate an Axis Army in Moscow")
        };
    }
}