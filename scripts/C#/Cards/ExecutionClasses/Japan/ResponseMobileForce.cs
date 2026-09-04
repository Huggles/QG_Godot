using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class ResponseMobileForce : ResponseCardLogic
{
    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            // "At the beginning of your turn" = the Play step with the play still unspent. See
            // StatusVolksturm for why this is not TurnStep.START — for a face-down Response card
            // the leak was the worst: the start window only ever opened for the faction holding
            // one, so its appearance identified the card. Activating it does not spend the play.
            //
            // IsFactionTurn is new. It was implied before (only CurrentFaction was prompted at
            // START) but IsPlayCardStep is true for every faction during anyone's Play step, which
            // would tag this activatable off-turn.
            Condition.Build(new Condition.IsPlayCardStep(), this),
            Condition.Build(new Condition.IsFactionTurn(Faction), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async() => {
                var northPacific = CountryState.ForEnum(Country.NorthPacific);
                var recruitableTargets = CountryState.RecruitableSea(Faction)
                    .Where(cs => cs == northPacific || northPacific.ConnectedCountryStates.Contains(cs))
                    .ToList();
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, recruitableTargets.ToCountryIds()).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.RECRUIT));
                deployUnitChangeEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(deployUnitChangeEvent);
            })
            .WithCondition(()=> Condition.Build(new Condition.CustomCondition(() => {
                var northPacific = CountryState.ForEnum(Country.NorthPacific);
                return CountryState.RecruitableSea(Faction).Any(cs => cs == northPacific || northPacific.ConnectedCountryStates.Contains(cs));
            }), this))
            .WithGuidance("Recruit a navy in or adjacent to the North Pacific"),
        }; 
    }
}