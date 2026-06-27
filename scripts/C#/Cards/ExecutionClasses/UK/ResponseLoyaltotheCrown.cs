using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class ResponseLoyaltotheCrown : ResponseCardLogic
{
    private static readonly List<Country> TargetCountries = [Country.India, Country.Australia, Country.Canada];

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.UnitAboutToBeDeployed(FactionTeam.AXIS, UnitType.ARMY)
                .WithCountries(TargetCountries), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async () => {
                var deployEvent = CardPlayPool.GetChangeEvents<DeployUnitChangeEvent>()
                    .Last(ce =>
                        StaticGameData.FactionTeamForFaction(ce.TriggeringFaction) == FactionTeam.AXIS
                        && ce.UnitType == UnitType.ARMY
                        && TargetCountries.Contains(ce.CountryState.Country)
                    );

                RemoveUnitChangeEvent removeEvent = BuildChangeEvent(
                    new RemoveUnitChangeEvent(Faction, deployEvent.UnitId, UnitRemovalReason.ELIMINATE));
                removeEvent.IsTrigger = true;
                return removeEvent;
            })
            .WithGuidance("Eliminate the Axis Army just built")
        };
    }
}
