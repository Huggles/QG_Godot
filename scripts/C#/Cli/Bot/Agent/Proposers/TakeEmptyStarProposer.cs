using System.Collections.Generic;

/// <summary>
/// Every empty supply star this faction could deploy into, valued at what standing there would actually
/// pay.
///
/// The value is <see cref="VpMath.VpDeltaOfDeploy"/> rather than a constant, which matters more than it
/// looks: an empty star is worth 2 VP/turn, but the same call returns 0 for a star the faction already
/// holds and the correct (smaller) number for one a teammate is on. Asking the formula means this
/// proposer never has to enumerate those cases, and cannot get them the wrong way round the way a
/// hand-written table did.
/// </summary>
public sealed class TakeEmptyStarProposer : IGoalProposer
{
    public GoalKind Kind => GoalKind.TakeEmptyStar;

    public void Propose(BoardAssessment assessment, double weight, List<Goal> into)
    {
        foreach (int countryId in assessment.TakeableStarIds)
        {
            CountryState country = CountryState.ForId(countryId);
            if (country == null) continue;

            int vpPerTurn = VpMath.VpDeltaOfDeploy(country, assessment.Faction);
            if (vpPerTurn <= 0) continue;

            into.Add(new Goal(
                Kind,
                countryId,
                vpPerTurn * assessment.Horizon * weight,
                $"empty star {country.Label} pays {vpPerTurn}/turn"));
        }
    }
}
