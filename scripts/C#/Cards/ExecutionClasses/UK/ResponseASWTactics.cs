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
            Condition.Build(new Condition.IsBlockRequest(), this),
            Condition.Build(new Condition.CustomCondition(() => {
                if (CardPlayPool.LastNoneNewCardChangeEvent is ForceDiscardCardsChangeEvent fde) {
                    return fde.HasSourceCard
                        && StaticGameData.FactionTeamForFaction(fde.SourceCardState.Faction) == FactionTeam.AXIS
                        && fde.SourceCardState.CardData.CardType == CardType.ECONOMIC_WARFARE;
                }
                return false;
            }), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async () => {
                CardPlayPool.LastNoneNewCardChangeEvent.IsBlocked = true;
                PresentationServices.Notification.ShowActionText("ASW Tactics: Axis EW card effect ignored", Faction);
                await Task.Delay(GameSettings.DurationMedium);
                return null;
            })
            .WithGuidance("Ignore the Axis EW card's game text")
        };
    }
}
