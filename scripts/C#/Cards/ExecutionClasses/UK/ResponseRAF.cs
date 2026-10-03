using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class ResponseRAF : ResponseCardLogic
{
    /// <summary>The UK piece in or beside the United Kingdom about to be removed. The block window has already named it, so this card picks nothing.</summary>
    public override TargetSet Targets() => BlockTargets();

    protected override List<Condition> CardTriggers()
    {
        List<int> ukAndAdjacent = new List<int> { CountryState.ForEnum(Country.UnitedKingdom).Id }
            .Concat(CountryState.ForEnum(Country.UnitedKingdom).ConnectedCountryIds)
            .ToList();

        return new List<Condition> {
            Condition.Build(new Condition.IsBlockRequest(), this),
            Condition.Build(new Condition.UnitAboutToBeRemoved(Faction.UNITED_KINGDOM)
                .WithCountries(ukAndAdjacent), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new BlockStep<RemoveUnitChangeEvent>(this, async removeEvent => {
                removeEvent.UnitState.ImmuneForTurn = true;
                await new ShowActionLabelPresentationEvent(Faction, $"{Faction.WithPlayer()} plays RAF: the UK piece will not be removed this turn").Apply();
                await Task.Delay(GameSettings.DurationMedium);
                return CardStepResult.Block();
            })
            .WithGuidance("Protect its piece in or adjacent to the United Kingdom from removal this turn")
        };
    }
}