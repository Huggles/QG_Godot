using System.Collections.Generic;

/// <summary>
/// Supply stars this faction holds that an enemy is in a position to attack, valued at the income that
/// would be lost.
///
/// Defence and offence end up denominated in the same unit, which is the reason to value it this way
/// rather than with a separate "danger" score: a star worth 2 VP/turn that is about to be taken is
/// exactly as urgent as an empty star worth 2 VP/turn is attractive, and the agenda can say so without
/// anybody tuning the two against each other.
///
/// This says nothing about HOW to hold it — reinforcing, clearing the attacker, or accepting the loss and
/// spending the turn elsewhere are all answers, and choosing between them belongs to whatever reads the
/// agenda, not here.
/// </summary>
public sealed class HoldThreatenedStarProposer : IGoalProposer
{
    public GoalKind Kind => GoalKind.HoldThreatenedStar;

    public void Propose(BoardAssessment assessment, double weight, List<Goal> into)
    {
        foreach (int countryId in assessment.ThreatenedStarIds)
        {
            CountryState country = CountryState.ForId(countryId);
            if (country == null) continue;

            int atRisk = -VpMath.VpDeltaOfRemoval(country, assessment.Faction);
            if (atRisk <= 0) continue;

            into.Add(new Goal(
                Kind,
                countryId,
                atRisk * assessment.Horizon * weight,
                $"{country.Label} is attackable and pays {atRisk}/turn"));
        }
    }
}
