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

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.IsBlockRequest(), this),
            Condition.Build(new Condition.CustomCondition(() => {
                if (CardPlayPool.CurrentBlockTrigger is not RemoveUnitChangeEvent removeEvent) return false;
                return removeEvent.UnitState.Faction == Faction
                    && removeEvent.UnitState.Type == UnitType.ARMY
                    && removeEvent.UnitState.InSupply
                    && targetCountryIds.Contains(removeEvent.CountryId);
            }), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async () => {
                if (ActivationTrigger is RemoveUnitChangeEvent removeEvent) {
                    removeEvent.IsBlocked = true;
                    removeEvent.UnitState.ImmuneForTurn = true;
                    PresentationServices.Notification.ShowActionText("Kwantung Army: Japanese Army will not be removed this turn", Faction);
                    await Task.Delay(GameSettings.DurationMedium);
                }
            })
            .WithGuidance("Do not remove your supplied Army in China, Szechuan, Mongolia, or Vladivostok")
        };
    }
}