using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// "Use at any time. Do not remove Soviet Armies in Siberia and/or Kazakhstan this turn." Offered in the
/// removal block window (decided): when a Soviet Army there is about to be removed by a battle, an
/// elimination or a supply loss, which is every moment the protection matters. It protects every Soviet
/// Army in both spaces for the rest of the turn and blocks that removal. A true "any time" window comes
/// with the Bolster windows.
/// </summary>
public partial class ResponseEndlessExpanses : ResponseCardLogic
{
    private static readonly List<int> Expanses = [(int)Country.Siberia, (int)Country.Kazakhstan];

    private static List<int> SovietArmiesThere(BoardState board) => board.ActiveUnits(Faction.SOVIET)
        .Where(unit => unit.IsArmy && Expanses.Contains(board.CountryOf(unit)))
        .Select(unit => unit.Id).ToList();

    /// <summary>Every Soviet Army it protects, not only the one about to be removed.</summary>
    public override TargetSet Targets() => TargetSet.Units(SovietArmiesThere(BoardState.Live));

    protected override List<Condition> CardTriggers() => new()
    {
        Condition.Build(new Condition.IsBlockRequest(), this),
        Condition.Build(new Condition.UnitAboutToBeRemoved(Faction.SOVIET, UnitType.ARMY).WithCountries(Expanses), this),
    };

    public override List<CardStep> OnActivate() => new()
    {
        new BlockStep<RemoveUnitChangeEvent>(this, async removeEvent => {
            await new ProtectUnitsChangeEvent(Faction, SovietArmiesThere(BoardState.Live)).Apply();
            await new ShowActionLabelPresentationEvent(Faction, $"{Faction.WithPlayer()} plays Endless Expanses: Soviet Armies in Siberia and Kazakhstan will not be removed this turn").Apply();
            await Task.Delay(GameSettings.DurationMedium);
            return CardStepResult.Block();
        })
        .WithGuidance("Protect the Soviet Armies in Siberia and Kazakhstan from removal this turn")
    };
}
