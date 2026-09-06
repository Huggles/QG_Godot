using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class ResponseRasputitsa : ResponseCardLogic
{
    /// <summary>The Axis Army just built near Moscow — the piece this eliminates. Chosen by the trigger, not by the player.</summary>
    public override TargetSet Targets() => TriggerTargets();

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.CustomCondition(()=>{
                var trigger = CardPlayPool.CurrentReactionTrigger as DeployUnitChangeEvent;
                if (trigger == null) return false;
                if (StaticGameData.FactionTeamForFaction(trigger.TriggeringFaction) != FactionTeam.AXIS) return false;
                if (trigger.UnitType != UnitType.ARMY) return false;
                var moscowCountry = CountryState.ForEnum(Country.Moscow);
                return trigger.CountryState.Country == Country.Moscow ||
                       moscowCountry.ConnectedCountryStates.Any(cs => cs.Country == trigger.CountryState.Country);
            }).InReactionWindow(), this),
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new CardStep(this, async () => {
                var trigger = CardPlayPool.CurrentReactionTrigger as DeployUnitChangeEvent;
                if (trigger == null) throw new Exception("Reaction trigger is not a DeployUnitChangeEvent");
                RemoveUnitChangeEvent removeEvent = BuildChangeEvent(
                    new RemoveUnitChangeEvent(Faction, trigger.UnitId, UnitRemovalReason.ELIMINATE));
                removeEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(removeEvent);
            })
            .WithGuidance("Eliminate the Axis Army just built near Moscow")
        };
    }
}