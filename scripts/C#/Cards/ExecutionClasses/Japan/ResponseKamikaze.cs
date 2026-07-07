using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class ResponseKamikaze : ResponseCardLogic
{
    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.HasDeployedNavy(FactionTeam.ALLIES).Immediately(), this),
            Condition.Build(new Condition.CustomCondition(() => {
                var trigger = CardPlayPool.CurrentReactionTrigger as DeployUnitChangeEvent;
                if (trigger == null) return false;
                return trigger.CountryState.ConnectedCountryStates
                    .Any(cs => cs.Units.ContainsKey(Faction));
            }), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async () => {
                var trigger = CardPlayPool.CurrentReactionTrigger as DeployUnitChangeEvent;
                if (trigger == null) return null;
                RemoveUnitChangeEvent removeEvent = BuildChangeEvent(
                    new RemoveUnitChangeEvent(Faction, trigger.UnitId, UnitRemovalReason.ELIMINATE));
                return removeEvent;
            })
            .WithGuidance("Eliminate the Allied Navy just built"),
        };
    }
}