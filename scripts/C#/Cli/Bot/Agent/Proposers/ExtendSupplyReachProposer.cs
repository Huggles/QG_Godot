using System.Collections.Generic;

/// <summary>
/// Countries that pay nothing themselves but put stars within reach that are not reachable now.
///
/// This is the only proposer whose goal does not score, and it exists because a bot that only ever values
/// income cannot cross a gap: deploying is gated on having an adjacent supplied unit
/// (<see cref="CountryState.CanBuild"/>), so a distant star stays invisible to every other goal here
/// until something is standing next to it. Staging is how the board opens up.
///
/// Valued at what it opens, discounted by <see cref="StagingDiscount"/>, because the payout is a turn
/// away and depends on still being able to take the star when it arrives. The discount is the one
/// deliberately soft number in the agenda; it is a statement that a bird in the bush is worth about half
/// a bird in the hand, and it is a single named constant precisely so it can be tuned against measurement
/// later instead of being spread across the valuation.
///
/// A star is only counted if it is not already reachable and not already held — staging toward somewhere
/// this faction can reach today is worth nothing extra. Enemy-held neighbours are skipped rather than
/// counted: they cannot be deployed into at all, so they are <see cref="GoalKind.ClearEnemyStar"/>'s
/// business, and counting them here would double-value the same square.
/// </summary>
public sealed class ExtendSupplyReachProposer : IGoalProposer
{
    /// <summary>How much of an opened star's value survives being one turn away. See the class summary.</summary>
    public const double StagingDiscount = 0.5;

    public GoalKind Kind => GoalKind.ExtendSupplyReach;

    public void Propose(BoardAssessment assessment, double weight, List<Goal> into)
    {
        if (assessment.BuildableNonStarIds.Count == 0) return;

        HashSet<int> alreadyReachable = new(assessment.TakeableStarIds);
        foreach (int heldId in assessment.HeldStarIds) alreadyReachable.Add(heldId);

        foreach (int countryId in assessment.BuildableNonStarIds)
        {
            CountryState country = CountryState.ForId(countryId);
            if (country == null) continue;

            int opened = 0;
            int openedCount = 0;

            foreach (int neighbourId in country.AdjacentCountryIds(assessment.Faction))
            {
                CountryState neighbour = CountryState.ForId(neighbourId);
                if (neighbour == null || !neighbour.IsSupply) continue;
                if (alreadyReachable.Contains(neighbourId)) continue;
                if (neighbour.OccupyingTeam == assessment.EnemyTeam) continue;

                int gain = VpMath.VpDeltaOfDeploy(neighbour, assessment.Faction);
                if (gain <= 0) continue;

                opened += gain;
                openedCount++;
            }

            if (opened <= 0) continue;

            into.Add(new Goal(
                Kind,
                countryId,
                opened * assessment.Horizon * StagingDiscount * weight,
                $"{country.Label} brings {openedCount} star(s) worth {opened}/turn into reach"));
        }
    }
}
