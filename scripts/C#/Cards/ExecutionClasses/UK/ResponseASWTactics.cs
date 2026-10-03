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
            // BlockStep<ChangeEvent>: this is the one blocker that does not care WHICH event it was
            // offered — the trigger already scopes it to an Axis Economic Warfare card, and the card
            // text is about that whole card rather than any one of its effects. BlockCard() is what
            // sets IsCardBlocked, which CardPlayRound.DoCard reads to stop the rest of its steps.
            //
            // The old body dereferenced ActivationTrigger bare; BlockStep now does the cast, so a
            // mismatch is a logged no-op rather than a NullReferenceException that halts the turn.
            new BlockStep<ChangeEvent>(this, async blockedEvent => {
                await new ShowActionLabelPresentationEvent(Faction, $"{Faction.WithPlayer()} plays ASW Tactics: the Axis Economic Warfare card has no effect").Apply();
                return CardStepResult.BlockCard();
            })
        };
    }
}
