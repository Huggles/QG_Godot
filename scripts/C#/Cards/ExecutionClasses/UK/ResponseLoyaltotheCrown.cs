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
            Condition.Build(new Condition.CustomCondition(() => {
                if (CardPlayPool.CurrentReactionTrigger is not DeployUnitChangeEvent deployEvent) return false;
                return StaticGameData.FactionTeamForFaction(deployEvent.TriggeringFaction) == FactionTeam.AXIS
                    && deployEvent.UnitType == UnitType.ARMY
                    && TargetCountries.Contains(deployEvent.CountryState.Country);
            }).InReactionWindow(), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async () => {
                var deployEvent = CardPlayPool.CurrentReactionTrigger as DeployUnitChangeEvent;
                if (deployEvent == null) return;

                RemoveUnitChangeEvent removeEvent = BuildChangeEvent(
                    new RemoveUnitChangeEvent(Faction, deployEvent.UnitId, UnitRemovalReason.ELIMINATE));
                removeEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(removeEvent);
            })
            .WithGuidance("Eliminate the Axis Army just built")
        };
    }
}
