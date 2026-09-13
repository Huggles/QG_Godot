using System.Collections.Generic;

/// <summary>
/// Units standing on a supply star with their supply line cut.
///
/// The SUPPLY step removes every out-of-supply active unit (see SupplyStepHandlerDefault), so unlike the
/// other goals here this one is not an opportunity — it is a loss already scheduled, and it lands before
/// the next victory step. Valued at the income that unit is currently earning and is about to stop
/// earning.
///
/// **This fires very rarely, and the reason is structural rather than a matter of luck.** Measured across
/// four seeds of Scenario_Basic it proposed nothing at all, which is close to correct:
/// GameAPI.GetSupplyCountryIds lists the supply countries a faction OCCUPIES, so a unit standing on its
/// own supply star is standing on a supply source, and CalculateSupplyForUnit finds a zero-length path to
/// it. An army on a star is in supply by definition. Only two cases escape that, and both are genuinely
/// uncommon:
///
///  - a NAVY on a supply sea space with no friendly harbour adjacent — CalculateSupplyForUnit has a
///    separate HasHarbor test for navies that the zero-length path does not satisfy;
///  - a star removed from the supply list by an <see cref="ISupplyBlockModifier"/> card.
///
/// It is kept because it is correct when it does fire and those two cases are real losses. It should NOT
/// be read as the bot's general answer to supply.
///
/// **Only units ON a star produce a goal, and that is the larger limitation.** A unit dying out of supply
/// somewhere that pays nothing still costs tempo, board presence, and a piece from a pool that caps
/// between 5 and 8 — but none of that is expressible in victory points, and the agenda's whole claim is
/// that its values are comparable because they are all VP. Inventing a number for a non-scoring unit
/// would break that claim for every other goal. Modelling tempo, and modelling the supply CHAIN rather
/// than the unit at the end of it, are separate pieces of work; until they exist this proposer stays
/// silent rather than guessing.
/// </summary>
public sealed class KeepUnitsInSupplyProposer : IGoalProposer
{
    public GoalKind Kind => GoalKind.KeepUnitsInSupply;

    public void Propose(BoardAssessment assessment, double weight, List<Goal> into)
    {
        if (assessment.UnitsOutOfSupply == 0) return;

        FactionState state = FactionState.ForEnum(assessment.Faction);
        if (state == null) return;

        foreach (int unitId in state.UnsuppliedUnitIds)
        {
            // UnitState.ForId indexes the id map directly and throws on an id a nested reaction has
            // already removed, so this goes through the state's own map instead of assuming.
            if (!GameSession.Current.GameState.UnitStatesById.TryGetValue(unitId, out UnitState unit)) continue;
            if (unit == null || unit.CountryId < 0) continue;

            CountryState country = CountryState.ForId(unit.CountryId);
            if (country == null) continue;

            int atRisk = -VpMath.VpDeltaOfRemoval(country, assessment.Faction);
            if (atRisk <= 0) continue;

            into.Add(new Goal(
                Kind,
                unit.CountryId,
                atRisk * assessment.Horizon * weight,
                $"unit on {country.Label} is out of supply and {atRisk}/turn dies with it"));
        }
    }
}
