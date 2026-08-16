using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class ResponseASWTactics : ResponseCardLogic
{
    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(
                new Condition.IsBlockRequest(CardType.ECONOMIC_WARFARE), 
                this
            )
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async () => {
                ActivationTrigger.IsBlocked = true;
                ActivationTrigger.IsCardBlocked = true;
                PresentationServices.Notification.ShowActionText("Axis EW card effect ignored", Faction);                
            })
        };
    }
}
