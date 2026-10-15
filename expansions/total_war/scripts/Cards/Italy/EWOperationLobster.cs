using System.Collections.Generic;

public partial class EWOperationLobster : EWCardLogic
{
    public override List<CardStep> OnActivate() => new()
    {
        new ResultStep(this, Choose.Fixed(_ => new ForceDiscardCardsChangeEvent(Faction, Faction.UNITED_KINGDOM, 2)))
            .WithGuidance("Make the United Kingdom discard the top 2 cards of its draw deck")
    };
}
