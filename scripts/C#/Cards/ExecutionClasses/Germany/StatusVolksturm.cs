using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class StatusVolksturm : StatusCardLogic
{
    /// <summary> "This is in addition to your Play step" — the recruit does not cost the hand card. </summary>
    public override bool IsFreePlayStepActivation => true;

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            // "At the beginning of your turn" is expressed as the Play step with the play still
            // unspent, not as TurnStep.START: a window that only opened when someone held a
            // start-step card announced that they held one. IsPlayCardStep also places the card
            // beside the hand in the play prompt (CardLogic.IsPlayStepActivation) — but this one
            // does NOT spend the play, per the card text.
            Condition.Build(new Condition.IsPlayCardStep(), this),
            Condition.Build(new Condition.IsFactionTurn(Faction), this),
            Condition.Build(new Condition.CountryIsRecruitable([(int)Country.Germany], Faction), this)
        };
    }

    public override List<CardStep> OnActivate() 
    {
        return new List<CardStep> {
            new CardStep(this, async() => {
                ForceDiscardCardsChangeEvent discardEvent = BuildChangeEvent(new ForceDiscardCardsChangeEvent(Faction, Faction, 1));
                discardEvent.IsTrigger = false;
                // No ShowModal here: ForceDiscardCardsChangeEvent.AfterAnimations already queues a
                // ShowDiscardModalAnimation for the same cards, and Apply awaits the animation
                // queue — so showing it again here displayed the discard modal twice.
                await discardEvent.Apply();

                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, new List<int>{(int)Country.Germany}).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.RECRUIT));
                deployUnitChangeEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(deployUnitChangeEvent);
            })
            .WithGuidance("Recruit an army in Germany (in addition to your playstep)")
            // Hollow when Germany already holds a German unit: the recruit redeploys the piece
            // standing there and the board is unchanged (see CountryState.CanBuild).
            .WithAdvisoryCondition(() => Condition.Build(new Condition.Not(new Condition.CountryHasFactionUnit((int)Country.Germany, Faction)), this))
        };
    }
}
