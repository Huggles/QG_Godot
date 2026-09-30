using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EventArsenalofDemocracy : EventCardLogic
{
    private static readonly Faction targetFaction = Faction.UNITED_KINGDOM;
    // Whether step 1 built an army, read off its outcome so step 2 offers only the other type. False
    // when step 1 has not run, which is what the field this replaces held before a first play.
    private static bool BuiltArmy(StepOption? first)
        => first is { Target: { } built } && CountryState.ForId(built.Id).Type == CountryType.LAND;

    private StepOption? FirstStepOutcome => CardSteps[0] is { StepSucceeded: true } first ? first.LastOutcome : null;

    /// <summary>
    /// Everywhere the UK could put either piece. Step 1 offers both types and step 2 offers whichever
    /// is left, so the union across the card is simply both — and the card acts on the UK, not on the
    /// US, which is why the preview lights UK build spaces.
    /// </summary>
    public override TargetSet Targets() =>
        TargetSet.Countries(BoardState.Live.BuildableLand(targetFaction))
            .Plus(TargetSet.Countries(BoardState.Live.BuildableSea(targetFaction)));

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            // Step 1: show ALL buildable countries (both land and sea); player chooses order.
            new ResultStep(this, Choose.CountryFrom(
                    c => c.Board.BuildableLand(targetFaction).ToCountryIds().Concat(c.Board.BuildableSea(targetFaction).ToCountryIds()).ToList(),
                    (countryId, _) => new DeployUnitChangeEvent(targetFaction, countryId, DeployType.BUILD)))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s =>
                s.Board.BuildableLand(targetFaction).Any() || s.Board.BuildableSea(targetFaction).Any()), this))
            .WithGuidance("United Kingdom builds an Army or a Navy (choose order)"),
            // Step 2: show only the other type to complete the pair.
            new ResultStep(this, Choose.CountryFrom(c => BuiltArmy(c.Previous)
                    ? c.Board.BuildableSea(targetFaction).ToCountryIds()
                    : c.Board.BuildableLand(targetFaction).ToCountryIds(),
                (countryId, _) => new DeployUnitChangeEvent(targetFaction, countryId, DeployType.BUILD)))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => {
                var buildable = BuiltArmy(FirstStepOutcome)
                    ? s.Board.BuildableSea(targetFaction)
                    : s.Board.BuildableLand(targetFaction);
                return buildable.Any();
            }), this))
            .WithGuidance("United Kingdom builds the other unit type"),
        };
    }
}