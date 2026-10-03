using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class ResponseKwantungArmy : ResponseCardLogic
{
    private static readonly List<int> targetCountryIds =
    [
        (int)Country.China,
        (int)Country.Szechuan,
        (int)Country.Mongolia,
        (int)Country.Vladivostok
    ];

    /// <summary>The supplied Japanese Army about to be removed. The block window has already named it, so this card picks nothing.</summary>
    public override TargetSet Targets() => BlockTargets();

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.IsBlockRequest(), this),
            Condition.Build(new Condition.CustomCondition(s => {
                if (s.BlockTrigger is not RemoveUnitChangeEvent removeEvent) return false;
                return removeEvent.UnitState.Faction == Faction
                    && removeEvent.UnitState.Type == UnitType.ARMY
                    && s.Board.InSupply(removeEvent.UnitState)
                    && targetCountryIds.Contains(removeEvent.CountryId);
            }), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new BlockStep<RemoveUnitChangeEvent>(this, async removeEvent => {
                removeEvent.UnitState.ImmuneForTurn = true;
                await new ShowActionLabelPresentationEvent(Faction, $"{Faction.WithPlayer()} plays Kwantung Army: the Japanese Army will not be removed this turn").Apply();
                await Task.Delay(GameSettings.DurationMedium);
                return CardStepResult.Block();
            })
            .WithGuidance("Protect its supplied Army in China, Szechuan, Mongolia, or Vladivostok from removal")
        };
    }
}