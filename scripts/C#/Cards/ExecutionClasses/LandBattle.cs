using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class LandBattle : CardLogic
{    
    /// <summary>
    /// A battle target is either an enemy army or an empty land country, so the offer has two halves.
    /// Both are read by the step's selection and by <see cref="Targets"/>, so the hover preview cannot
    /// drift from the real offer.
    /// </summary>
    private List<int> AttackableArmies(BoardState board) => board.AttackableArmyIds(Faction);

    /// <inheritdoc cref="AttackableArmies"/>
    private List<int> AttackableCountries(BoardState board) => board.AttackableLandIds(Faction);

    public override TargetSet Targets() =>
        TargetSet.Countries(AttackableCountries(BoardState.Live)).Plus(TargetSet.Units(AttackableArmies(BoardState.Live)));

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            // Countries then units: the list constructor splits by type, so this offers exactly the old two lists.
            new ResultStep(this, Choose.BattleTargetFrom(c => AttackableCountries(c.Board).Select(id => new BattleTarget(id, TargetType.COUNTRY))
                    .Concat(AttackableArmies(c.Board).Select(id => new BattleTarget(id, TargetType.UNIT))).ToList(),
                (target, _) => target.ToAttackChangeEvent(Faction)))
            .WithCondition(()=> Condition.Build(new Condition.HasLandBattleTarget(Faction), this))
            .WithGuidance("Select a army or empty land country to attack")
        }; 
    }
}
