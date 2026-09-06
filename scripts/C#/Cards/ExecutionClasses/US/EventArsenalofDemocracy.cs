using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EventArsenalofDemocracy : EventCardLogic
{
    private static readonly Faction targetFaction = Faction.UNITED_KINGDOM;
    // Tracks which type was built first so step 2 offers only the other type.
    private bool _firstWasArmy;

    /// <summary>
    /// Everywhere the UK could put either piece. Step 1 offers both types and step 2 offers whichever
    /// is left, so the union across the card is simply both — and the card acts on the UK, not on the
    /// US, which is why the preview lights UK build spaces.
    /// </summary>
    public override TargetSet Targets() =>
        TargetSet.Countries(CountryState.BuildableLand(targetFaction))
            .Plus(TargetSet.Countries(CountryState.BuildableSea(targetFaction)));

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            // Step 1: show ALL buildable countries (both land and sea); player chooses order.
            new CardStep(this, async() => {
                var buildableLand = CountryState.BuildableLand(targetFaction).ToCountryIds();
                var buildableSea = CountryState.BuildableSea(targetFaction).ToCountryIds();
                var all = buildableLand.Concat(buildableSea).ToList();
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(targetFaction, all).BroadCast()).ResponseCountryIds[0];
                _firstWasArmy = CountryState.ForId(selectedCountryId).Type == CountryType.LAND;
                DeployUnitChangeEvent deployEvent = BuildChangeEvent(new DeployUnitChangeEvent(targetFaction, selectedCountryId, DeployType.BUILD));
                deployEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(deployEvent);
            })
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(() =>
                CountryState.BuildableLand(targetFaction).Any() || CountryState.BuildableSea(targetFaction).Any()), this))
            .WithGuidance("United Kingdom builds an Army or a Navy (choose order)"),
            // Step 2: show only the other type to complete the pair.
            new CardStep(this, async() => {
                var buildable = _firstWasArmy
                    ? CountryState.BuildableSea(targetFaction).ToCountryIds()
                    : CountryState.BuildableLand(targetFaction).ToCountryIds();
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(targetFaction, buildable).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployEvent = BuildChangeEvent(new DeployUnitChangeEvent(targetFaction, selectedCountryId, DeployType.BUILD));
                deployEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(deployEvent);
            })
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(() => {
                var buildable = _firstWasArmy
                    ? CountryState.BuildableSea(targetFaction)
                    : CountryState.BuildableLand(targetFaction);
                return buildable.Any();
            }), this))
            .WithGuidance("United Kingdom builds the other unit type"),
        };
    }
}