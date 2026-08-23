using System.Collections.Generic;

/// <summary>
/// What a <see cref="CardStep"/> could reach on the board if it ran right now, for the hover preview
/// that lights up a card's targets while the player is still choosing which card to play.
///
/// Presentation only. Nothing in the execution path reads it — the same relationship
/// <see cref="CardStep.MeetAllAdvisoryConditions"/> has to <see cref="CardStep.MeetAllConditions"/>.
/// A step that declares no preview previews nothing, which is the correct degraded behaviour rather
/// than a bug: the board simply stays dark for that card.
///
/// Unit ids are carried separately from country ids rather than pre-resolved, because a step that
/// targets a unit is describing something narrower than "this country" and a later, more precise
/// presentation (marking the unit itself) needs to know which it was.
/// </summary>
public sealed record StepTargetPreview(List<int> CountryIds, List<int> UnitIds)
{
    public static StepTargetPreview Countries(List<int> countryIds) =>
        new(countryIds ?? new List<int>(), new List<int>());

    public static StepTargetPreview Units(List<int> unitIds) =>
        new(new List<int>(), unitIds ?? new List<int>());

    /// <summary>
    /// For a step whose one selection spans both — a battle target, which is either an enemy unit or
    /// an empty country (see <c>LandBattle</c> / <c>SeaBattle</c>).
    /// </summary>
    public static StepTargetPreview Both(List<int> countryIds, List<int> unitIds) =>
        new(countryIds ?? new List<int>(), unitIds ?? new List<int>());
}
