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
            Condition.Build(new Condition.CustomCondition(s => {
                var trigger = s.ReactionTrigger as DeployUnitChangeEvent;
                if (trigger == null) return false;
                return trigger.CountryState.ConnectedCountryStates
                    .Any(cs => s.Board.UnitsIn(cs).ContainsKey(Faction));
            }), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new ResultStep(this, Choose.Fixed(c => {
                var trigger = c.Situation.ReactionTrigger as DeployUnitChangeEvent;
                if (trigger == null) return null;
                return new RemoveUnitChangeEvent(Faction, trigger.UnitId, UnitRemovalReason.ELIMINATE, c.Board);
            }))
            .WithGuidance("Eliminate the Allied Navy just built"),
        };
    }
}