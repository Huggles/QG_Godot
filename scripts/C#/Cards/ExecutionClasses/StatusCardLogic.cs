using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public abstract partial class StatusCardLogic : CardLogic
{
    public override List<CardStep> InitializePlayCardSteps()
    {
        return new List<CardStep> {
            new CardStep(this, async() => {
                // Card is placed in StatusCardIds by DeckState.PlayCard()
                await Task.CompletedTask;
                return null;
            })
        };
    }
}
