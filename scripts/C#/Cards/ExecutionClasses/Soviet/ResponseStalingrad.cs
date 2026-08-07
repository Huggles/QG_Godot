using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class ResponseStalingrad : ResponseCardLogic
{
    List<int> targetCountries = [(int)Country.Ukraine];

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.IsBlockRequest(), this),
            Condition.Build(new Condition.UnitAboutToBeRemoved(Faction.SOVIET, UnitType.ARMY)
                .WithCountries(targetCountries), this),
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new CardStep(this, async ()=>{
                if(CardPlayPool.LastNoneNewCardChangeEvent is RemoveUnitChangeEvent removeEvent){
                    removeEvent.IsBlocked = true;
                    removeEvent.UnitState.ImmuneForTurn = true;
                    PresentationServices.Notification.ShowActionText($"{FactionState.ForEnum(Faction).FactionData.Label} prevented the removal of his army in {CountryState.ForId(targetCountries[0])}", Faction);
                    await Task.Delay(GameSettings.DurationMedium);
                }
                return null;
            })
            .WithGuidance("Do not remove your Army in Ukraine this turn")
        };
    }
}
