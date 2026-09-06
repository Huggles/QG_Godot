using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class ResponseKamikaze : ResponseCardLogic
{
    /// <summary>The Allied Navy just built beside a Japanese piece — the one this eliminates. Chosen by the trigger, not by the player.</summary>
    public override TargetSet Targets() => TriggerTargets();

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
                if (trigger == null) return;
                RemoveUnitChangeEvent removeEvent = BuildChangeEvent(
                    new RemoveUnitChangeEvent(Faction, trigger.UnitId, UnitRemovalReason.ELIMINATE));
                removeEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(removeEvent);
            })
            .WithGuidance("Eliminate the Allied Navy just built"),
        };
    }
}