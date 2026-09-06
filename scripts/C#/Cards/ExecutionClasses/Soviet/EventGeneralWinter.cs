using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class EventGeneralWinter : EventCardLogic
{
    private List<int> AxisArmiesNearMoscow
    {
        get
        {
            var moscowCs = CountryState.ForEnum(Country.Moscow);
            return new List<CountryState> { moscowCs }
                .Concat(moscowCs.ConnectedCountryStates)
                .SelectMany(cs => cs.Units.Values)
                .Where(uId => {
                    var u = UnitState.ForId(uId);
                    return StaticGameData.FactionTeamForFaction(u.Faction) == FactionTeam.AXIS && u.IsArmy && !u.ImmuneForTurn;
                })
                .ToList();
        }
    }

    /// <summary>Both steps eliminate from this same list.</summary>
    public override TargetSet Targets() => TargetSet.Units(AxisArmiesNearMoscow);

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async () => {
                int selectedUnitId = (await new InputRequest.SelectUnitRequestHandler(Faction, AxisArmiesNearMoscow).BroadCast()).ResponseUnitIds[0];
                RemoveUnitChangeEvent removeEvent = BuildChangeEvent(new RemoveUnitChangeEvent(Faction, selectedUnitId, UnitRemovalReason.ELIMINATE));
                removeEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(removeEvent);
            })
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(() => AxisArmiesNearMoscow.Count > 0), this))
            .WithGuidance("Eliminate an Axis Army in or adjacent to Moscow"),

            new CardStep(this, async () => {
                int selectedUnitId = (await new InputRequest.SelectUnitRequestHandler(Faction, AxisArmiesNearMoscow).BroadCast()).ResponseUnitIds[0];
                RemoveUnitChangeEvent removeEvent = BuildChangeEvent(new RemoveUnitChangeEvent(Faction, selectedUnitId, UnitRemovalReason.ELIMINATE));
                removeEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(removeEvent);
            })
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(() => AxisArmiesNearMoscow.Count > 0), this))
            .WithGuidance("Eliminate a second Axis Army in or adjacent to Moscow")
        };
    }
}