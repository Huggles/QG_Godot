using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class SeaBattle : CardLogic
{
    /// <summary>
    /// A battle target is either an enemy navy or an empty sea country, so the offer has two halves.
    /// Both are read by the step's selection and by <see cref="Targets"/>, so the hover preview cannot
    /// drift from the real offer.
    /// </summary>
    private List<int> AttackableNavies(BoardState board) => board.AttackableNavyIds(Faction);

    /// <inheritdoc cref="AttackableNavies"/>
    private List<int> AttackableCountries(BoardState board) => board.AttackableSeaIds(Faction);

    public override TargetSet Targets() =>
        TargetSet.Countries(AttackableCountries(BoardState.Live)).Plus(TargetSet.Units(AttackableNavies(BoardState.Live)));

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            // Countries then units: the list constructor splits by type, so this offers exactly the old two lists.
            new ResultStep(this, Choose.BattleTargetFrom(c => AttackableCountries(c.Board).Select(id => new BattleTarget(id, TargetType.COUNTRY))
                    .Concat(AttackableNavies(c.Board).Select(id => new BattleTarget(id, TargetType.UNIT))).ToList(),
                (target, _) => target.ToAttackChangeEvent(Faction)))
            .WithCondition(()=>Condition.Build(new Condition.HasSeaBattleTarget(Faction), this))
            .WithGuidance("Select a navy or empty sea country to attack")
        }; 
    }
}
