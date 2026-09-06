using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class ResponseASWTactics : ResponseCardLogic
{
    /// <summary>
    /// The Axis EW card being blocked. A Card target names no board space — the preview draws only
    /// Country and Unit — so this lights nothing on the map; it is declared because the thing this
    /// card acts on genuinely is that card, and the CLI reads the same set.
    /// </summary>
    public override TargetSet Targets()
    {
        int sourceCardId = BlockContext?.SourceCardId ?? -1;
        return sourceCardId < 0 ? TargetSet.None : TargetSet.Cards(new List<int> { sourceCardId });
    }

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
