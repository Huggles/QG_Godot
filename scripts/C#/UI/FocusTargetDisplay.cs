using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Marks the unit a reaction window's trigger reached, so the focus viewport beside the trigger card
/// says WHICH unit and not merely which country.
///
/// Units only. The focus camera frames the country, so the country needs no mark of its own — and
/// glowing it would drown the one thing this is for. An after-reaction on a removal therefore marks
/// nothing: the unit is gone, and the country framing is the whole answer there.
///
/// Static rather than a node, and it raises <see cref="Tag.FocusTarget"/> rather than touching a scene:
/// exactly the shape and the reasoning of <see cref="CardTargetPreviewDisplay"/>, which does the same
/// job for the card hover preview. The visual belongs to <see cref="UnitScene"/>, which is also where
/// the decision to draw it in the focus viewport alone lives.
///
/// A separate tag from <see cref="Tag.PreviewTarget"/> on purpose — see that tag's own remarks. It is
/// what lets the two coexist on one unit without either clearing the other.
/// </summary>
public static class FocusTargetDisplay
{
    /// <summary>
    /// Exactly the units this class raised the tag on, so teardown cannot clear a tag somebody else
    /// raised or miss one whose target set has since changed.
    /// </summary>
    private static List<int> markedUnitIds = new();

    /// <summary>
    /// Mark these units for as long as the prompt is open. Replaces any previous set — one prompt is
    /// open at a time, so one set is.
    /// </summary>
    public static void Show(IEnumerable<int> unitIds)
    {
        Clear();
        if (unitIds == null) return;

        // Resolved and filtered rather than passed straight to AddTag: these ids arrived over the wire
        // on InputRequest.TriggerTargetUnitIds, and the tag extensions dereference every element.
        //
        // A unit no longer standing in a country is dropped too: CountryScene.RemoveUnit parks a
        // removed UnitScene off-board and hides it, so marking it would raise a tag on something that
        // cannot be seen and would still have to be cleared later.
        List<UnitState> unitStates = Resolve(unitIds)
            .Where(unitState => unitState.IsDeployedToCountry)
            .ToList();

        markedUnitIds = unitStates.Select(unitState => unitState.Id).ToList();
        unitStates.AddTag(Tag.FocusTarget, Faction.ALL);
    }

    /// <summary>Drop the marks. Called when the prompt they belong to closes.</summary>
    public static void Clear()
    {
        if (markedUnitIds.Count == 0) return;

        Resolve(markedUnitIds).RemoveTag(Tag.FocusTarget, Faction.ALL);
        markedUnitIds = new List<int>();
    }

    private static List<UnitState> Resolve(IEnumerable<int> unitIds) =>
        UnitState.ForIds(unitIds.Distinct()).Where(unitState => unitState != null).ToList();
}
