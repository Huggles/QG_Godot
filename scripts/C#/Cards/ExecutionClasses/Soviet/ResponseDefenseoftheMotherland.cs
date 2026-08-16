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

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.IsStartStep(), this),
            Condition.Build(new Condition.IsFactionTurn(Faction), this),
            Condition.Build(new Condition.CountryIsRecruitable(MoscowAndAdjacentIds, Faction), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async () => {
                int countryId = (await new InputRequest.SelectCountryRequestHandler(Faction, RecruitableNearMoscowIds).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, countryId, DeployType.RECRUIT));
                deployEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(deployEvent);
            })
            .WithCondition(() => Condition.Build(new Condition.CountryIsRecruitable(MoscowAndAdjacentIds, Faction), this))
            .WithGuidance("Recruit an Army in or adjacent to Moscow"),

            new CardStep(this, async () => {
                int unitId = (await new InputRequest.SelectUnitRequestHandler(Faction, AxisArmiesInMoscow).BroadCast()).ResponseUnitIds[0];
                RemoveUnitChangeEvent removeEvent = BuildChangeEvent(new RemoveUnitChangeEvent(Faction, unitId, UnitRemovalReason.ELIMINATE));
                removeEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(removeEvent);
            })
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(() => AxisArmiesInMoscow.Count > 0), this))
            .WithGuidance("Eliminate an Axis Army in Moscow")
        };
    }
}