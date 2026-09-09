using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EventBroadFront : EventCardLogic
{
    private int battlesCompleted = 0;
    private const int MaxBattles = 3;

    /// <summary>
    /// The Soviet Armies that qualified when this turn began, and the turn they were photographed on.
    ///
    /// The card fixes its own eligible set: "a space that was occupied by a Soviet Army adjacent to a
    /// German Army AT THE BEGINNING OF THE TURN". Reading that live meant the set moved underneath the
    /// card — its own battles change adjacency, so a space could become eligible part-way through, or
    /// stop being eligible before the player got to it. Neither is what the text describes.
    ///
    /// -1 means no photograph has been taken yet. See <see cref="EligibleSovietArmyIds"/>.
    /// </summary>
    private int snapshotTurn = -1;
    private List<int> snapshotSovietArmyIds = new();

    /// <summary>
    /// Photograph the board for the turn that is starting, and re-arm the battle counter with it.
    ///
    /// Runs on every peer and in replay (ChangeRoundChangeEvent drives it), so every peer holds the
    /// same set. Deliberately records adjacency ONLY: supply is a live question answered at battle
    /// time by <see cref="QualifyingTargets"/>, and this method runs beside the SuppliedForTurn reset
    /// in ChangeRoundChangeEvent.ResetPerTurnState, where supply state is mid-flight anyway.
    ///
    /// battlesCompleted resets here because the allowance is three battles per PLAY, and a play cannot
    /// span turns. Without it a card returned to hand — Guards and Flexible Resources both do that —
    /// would come back having already "used" its three.
    /// </summary>
    public override void OnNewTurnStarted(int turnNumber)
    {
        base.OnNewTurnStarted(turnNumber);

        snapshotTurn = turnNumber;
        snapshotSovietArmyIds = SovietArmiesAdjacentToGermanArmy();
        battlesCompleted = 0;
    }

    /// <summary>
    /// Soviet Armies standing next to a German Army. The card's own clause, and nothing else — no
    /// supply term, because the text does not put one here and supply is enforced live below.
    /// </summary>
    private List<int> SovietArmiesAdjacentToGermanArmy()
    {
        HashSet<int> germanArmyCountryIds = FactionState.ForEnum(Faction).ActiveUnitIds.ToUnitStates()
            .Where(us => us.Type == UnitType.ARMY)
            .Select(us => us.CountryId)
            .ToHashSet();

        return FactionState.ForEnum(Faction.SOVIET).ActiveUnitIds.ToUnitStates()
            .Where(us => us.Type == UnitType.ARMY
                      && CountryState.ForId(us.CountryId).ConnectedCountryStates
                             .Any(adj => germanArmyCountryIds.Contains(adj.Id)))
            .Select(us => us.Id)
            .ToList();
    }

    /// <summary>
    /// The photographed set, or a live read when no photograph exists — a save restored mid-turn, or a
    /// card asked before its first ChangeRoundChangeEvent. Falling back to the old live behaviour is
    /// wrong in the same small way it always was; offering nothing would make the card unplayable,
    /// which is worse, and the next turn corrects it.
    /// </summary>
    private List<int> EligibleSovietArmyIds =>
        snapshotTurn >= 0 ? snapshotSovietArmyIds : SovietArmiesAdjacentToGermanArmy();

    /// <summary>
    /// What can actually be battled right now: the turn-start set, intersected with the units this
    /// faction is currently able to attack.
    ///
    /// The intersection is what enforces supply. Tag.Attackable is built in
    /// GameStateCalculator.CalculateAttackableForFaction from GameAPI.SuppliedUnitsForFaction, so it
    /// is the same gate LandBattle and the other fifteen battle cards go through — this card was the
    /// only one hand-rolling adjacency and dropping the supply term with it. That let Germany battle
    /// from an unsupplied Army, which is not a legal battle, and it also handed StatusBlitzkrieg a
    /// space it could never build in: the space came up empty (so Blitzkrieg fired) but had no
    /// adjacent SUPPLIED German Army (so CanBuild refused), costing a card and a VP for nothing.
    ///
    /// It also drops units that have already died — including to this card's own earlier battles — so
    /// the "up to 3" allowance cannot be spent twice on the same Army.
    /// </summary>
    private List<BattleTarget> QualifyingTargets()
    {
        HashSet<int> attackableNow = UnitState.AttackableArmyIds(Faction).ToHashSet();

        return EligibleSovietArmyIds
            .Where(id => attackableNow.Contains(id))
            .Select(id => new BattleTarget(id, TargetType.UNIT))
            .ToList();
    }

    /// <summary>
    /// The Soviet Armies in reach right now. Later battles run against a board this one has already
    /// changed, and the steps for them do not exist yet at hover time — so this reports the first
    /// battle's offer, which is the only one that is knowable.
    /// </summary>
    public override TargetSet Targets() => TargetSet.FromBattleTargets(QualifyingTargets());

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
            await CardPlayPool.DoChangeEvent(battleEvent);
        })
        .WithCondition(() => Condition.Build(new Condition.CustomCondition(() =>
            battlesCompleted < MaxBattles && QualifyingTargets().Count > 0), this))
        .WithGuidance("Battle a Soviet Army adjacent to a German Army");
    }
}
