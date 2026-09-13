using System.Collections.Generic;

/// <summary>
/// Enemy-held supply stars this faction can attack, valued at the income taking a unit off would DENY
/// rather than at anything it gains.
///
/// Denial is the whole point, and it is why this is a separate goal from <see cref="GoalKind.TakeEmptyStar"/>:
/// a country locks to its first occupier's team, so there is no way to move onto an enemy star. Clearing
/// it pays nothing this turn; it pays by cutting their rate now and leaving an empty star that
/// TakeEmptyStar will pick up on a later turn, once the board actually shows it empty.
///
/// Where several enemy factions share a star, only ONE of them can be removed by a single battle, so the
/// goal is valued at the best single removal — not the sum. Valuing it at the total would make a crowded
/// star look like a jackpot and pull the whole agenda toward a fight that cannot deliver it in one go.
/// </summary>
public sealed class ClearEnemyStarProposer : IGoalProposer
{
    public GoalKind Kind => GoalKind.ClearEnemyStar;

    public void Propose(BoardAssessment assessment, double weight, List<Goal> into)
    {
        foreach (int countryId in assessment.AttackableEnemyStarIds)
        {
            CountryState country = CountryState.ForId(countryId);
            if (country == null) continue;

            Faction bestTarget = Faction.NONE;
            int bestDenied = 0;

            foreach (Faction occupant in country.Units.Keys)
            {
                if (StaticGameData.FactionTeamForFaction(occupant) != assessment.EnemyTeam) continue;

                // Sign flip: VpDeltaOfRemoval reports the change to the OWNER's team, which is negative
                // for them and is exactly what we are trying to cause.
                int denied = -VpMath.VpDeltaOfRemoval(country, occupant);
                if (denied <= 0) continue;

                // Ties break on the faction enum, never on encounter order. Units is a Dictionary, and
                // letting its enumeration order pick the target would make the agenda depend on the
                // order units happened to arrive rather than on the board.
                bool better = bestTarget == Faction.NONE
                              || denied > bestDenied
                              || (denied == bestDenied && occupant < bestTarget);
                if (!better) continue;

                bestTarget = occupant;
                bestDenied = denied;
            }

            if (bestTarget == Faction.NONE) continue;

            into.Add(new Goal(
                Kind,
                countryId,
                bestDenied * assessment.Horizon * weight,
                $"clearing {bestTarget} from {country.Label} denies {bestDenied}/turn"));
        }
    }
}
