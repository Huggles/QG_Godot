using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The StatusSyntheticFuel effect as a scenario rule every faction has, rather than a German card:
/// when you build an Army, you may discard the top 2 cards of your draw deck to build a second Army
/// adjacent to it.
///
/// "Triggers on any faction" is emergent — RegisterActivatableMutator creates one instance per
/// eligible faction, and each instance triggers on its own owner's build. Whoever builds is the one
/// offered the effect and the one who pays for it, so Faction here is always the acting faction and
/// the body below is StatusSyntheticFuel's unchanged. A rule that needs a genuinely global gate
/// ("only one faction may use this per round") would need shared state across the instances.
/// </summary>
public partial class MutatorSyntheticFuelAnyFaction : ActivatableMutator
{
    public override string Label => "Synthetic Fuel";

    public override string Text =>
        "Use once per turn when you build an Army. Discard the top 2 cards of your draw deck to build an Army adjacent to the Army just built.";

    protected override List<Condition> MutatorTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.FactionDeployed(Faction, DeployType.BUILD), this).Immediately(),
            Condition.Build(
                new Condition.CountryIsBuildable(
                    DeployTargets.ToCountryIds(),
                    Faction),
                this
            )
        };
    }

    public List<CountryState> DeployTargets
    {
        get
        {
            var trigger = CardPlayPool.CurrentReactionTrigger as DeployUnitChangeEvent;
            if (trigger == null) return new List<CountryState>();
            return CountryState.ForId(trigger.CountryId).AdjacentCountryStates(Faction)
                .Where(countryState => countryState.CanBuild(Faction) && countryState.IsLand)
                .Distinct()
                .ToList();
        }
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async() => {
                DebugUtilities.PrintPeer("MutatorSyntheticFuelAnyFaction react step");
                ForceDiscardCardsChangeEvent discardEvent = BuildChangeEvent(new ForceDiscardCardsChangeEvent(Faction, Faction, 2));
                discardEvent.IsTrigger = false;
                await CardPlayPool.DoChangeEvent(discardEvent);

                int countryId = (await new InputRequest.SelectCountryRequestHandler(Faction, DeployTargets.ToCountryIds()).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, countryId, DeployType.BUILD));
                deployUnitChangeEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(deployUnitChangeEvent);
            }).WithGuidance("Deploy an army adjacent to where you've deployed an army this turn")
        };
    }
}
