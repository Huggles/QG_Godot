using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EventBroadFront : EventCardLogic
{
    private int battlesCompleted = 0;
    private const int MaxBattles = 3;

    private List<BattleTarget> QualifyingTargets()
    {
        var germanArmyCountryIds = FactionState.ForEnum(Faction).ActiveUnitIds.ToUnitStates()
            .Where(us => us.Type == UnitType.ARMY)
            .Select(us => us.CountryId)
            .ToHashSet();

        FactionState.ForEnum(Faction.SOVIET).ActiveUnitIds.ToUnitStates()
            .Where(us => us.Type == UnitType.ARMY)
            .Select(us => us.CountryId)
            .ToHashSet();

        return FactionState.ForEnum(Faction.SOVIET).ActiveUnitIds.ToUnitStates()
            .Where(us => us.Type == UnitType.ARMY
                      && CountryState.ForId(us.CountryId).ConnectedCountryStates
                             .Any(adj => germanArmyCountryIds.Contains(adj.Id)))
            .Select(us => new BattleTarget(us.Id, TargetType.UNIT))
            .ToList();
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> { MakeBattleStep() };
    }

    private CardStep MakeBattleStep()
    {
        return new CardStep(this, async () =>
        {
            var targets = QualifyingTargets();
            var resp = await new InputRequest.SelectBattleTargetRequestHandler(Faction, targets).BroadCast();
            BattleTarget battleTarget = new BattleTarget(resp.ResponseUnitIds[0], TargetType.UNIT);
            battlesCompleted++;
            if (battlesCompleted < MaxBattles && QualifyingTargets().Count > 0)
                CardSteps.Add(MakeBattleStep());
            BattleCountryChangeEvent battleEvent = BuildChangeEvent(battleTarget.ToAttackChangeEvent(Faction));
            battleEvent.IsTrigger = true;
            return battleEvent;
        })
        .WithCondition(() => Condition.Build(new Condition.CustomCondition(() =>
            battlesCompleted < MaxBattles && QualifyingTargets().Count > 0), this))
        .WithGuidance("Battle a Soviet Army adjacent to a German Army");
    }
}