using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public abstract partial class ResponseCardLogic : CardLogic
{
    public override List<CardStep> InitializePlayCardSteps()
    {
        return new List<CardStep> {
            new CardStep(this, async() => {
                // Card is placed in ResponseCardIds by DeckState.PlayCard()
                await Task.CompletedTask;
                return null;
            })
        };
    }
}
