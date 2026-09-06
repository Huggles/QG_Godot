using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class ResponseLeningrad : ResponseCardLogic
{
    List<int> targetCountries = [(int)Country.Russia];

    /// <summary>The Soviet Army in Russia about to be removed. The block window has already named it, so this card picks nothing.</summary>
    public override TargetSet Targets() => BlockTargets();

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
                if(ActivationTrigger is RemoveUnitChangeEvent removeEvent){
                    removeEvent.IsBlocked = true;
                    removeEvent.UnitState.ImmuneForTurn = true;
                    PresentationServices.Notification.ShowActionText($"{Faction.WithPlayer()} prevented the removal of his army in {CountryState.ForId(targetCountries[0])}", Faction);
                    await Task.Delay(GameSettings.DurationMedium);
                }
            })
            .WithGuidance("Do not remove your Army in Russia this turn")
        };
    }
}
