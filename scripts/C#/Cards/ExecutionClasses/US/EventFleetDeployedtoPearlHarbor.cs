using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EventFleetDeployedtoPearlHarbor : EventCardLogic
{
    private static readonly List<int> anchorCountryIds = [(int)Country.Hawaii];

    /// <summary>The sea spaces the navy may actually be built in — the same filtered list step 2
    /// offers, so the preview and the offer cannot disagree.</summary>
    private List<CountryState> AdjacentNavyTargets(BoardState board) =>
        board.BuildableSea(Faction)
            .Where(cs => CountryState.ForEnum(Country.Hawaii).ConnectedCountryStates.Contains(cs))
            .ToList();

    /// <summary>Hawaii for the army, and the sea spaces beside it for the navy.</summary>
    public override TargetSet Targets() =>
        TargetSet.Countries(anchorCountryIds).Plus(TargetSet.Countries(AdjacentNavyTargets(BoardState.Live)));

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new ResultStep(this, Choose.CountryFrom(_ => anchorCountryIds,
                (countryId, _) => new DeployUnitChangeEvent(Faction, countryId, DeployType.RECRUIT)))
            .WithCondition(() => Condition.Build(new Condition.CountryIsRecruitable(anchorCountryIds, Faction), this))
            .WithGuidance("Recruit an Army in Hawaii"),
            new ResultStep(this, Choose.CountryFrom(c => AdjacentNavyTargets(c.Board).ToCountryIds(),
                (countryId, _) => new DeployUnitChangeEvent(Faction, countryId, DeployType.BUILD)))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => {
                return AdjacentNavyTargets(s.Board).Count > 0;
            }), this))
            .WithGuidance("Build a Navy adjacent to Hawaii"),
        };
    }
}