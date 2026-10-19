using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The menus' expansion checkboxes, one per expansion this install has found, built in code beside an
/// existing checkbox (the opening discard) so no scene file needs to know expansions exist. An install
/// without any expansion shows nothing at all.
///
/// Only an expansion that is available here can be ticked. One that is present but cannot be played
/// (not owned, or it failed to load) shows greyed out, with the reason as its tooltip.
/// </summary>
public sealed class ExpansionToggles
{
    private readonly Dictionary<string, CheckBox> _boxes = new();
    private bool _editable = true;

    public ExpansionToggles(CheckBox styleFrom)
    {
        Control after = styleFrom;
        foreach (LoadedExpansion expansion in ExpansionRegistry.All)
        {
            // A plain copy of the opening-discard box, for its layout and colours: no signals, groups or
            // script come along, so none of that box's handlers fire for this one.
            CheckBox box = (CheckBox)styleFrom.Duplicate(0);
            box.Name = $"Expansion_{expansion.Manifest.Id}";
            box.UniqueNameInOwner = false;
            box.Visible = true; // the host dialog copies a hidden template
            box.Text = $"Expansion: {expansion.Manifest.DisplayName}";
            box.ButtonPressed = false;
            box.TooltipText = expansion.UnavailableReason ?? string.Empty;

            after.AddSibling(box);
            after = box;
            _boxes[expansion.Manifest.Id] = box;
        }
        Refresh();
    }

    /// <summary>The ticked expansions, in <see cref="ExpansionIds"/> order.</summary>
    public List<string> Checked => ExpansionIds.Normalize(_boxes.Where(kv => kv.Value.ButtonPressed).Select(kv => kv.Key));

    /// <summary>
    /// Tick exactly those of <paramref name="ids"/> this install can play. A scenario naming an
    /// expansion that is not available here simply starts without it.
    /// </summary>
    public void Set(IEnumerable<string> ids)
    {
        HashSet<string> wanted = new(ids ?? Enumerable.Empty<string>());
        foreach ((string id, CheckBox box) in _boxes)
            box.SetPressedNoSignal(wanted.Contains(id) && ExpansionRegistry.Find(id)?.IsAvailable == true);
    }

    /// <summary>Whether this player may change the boxes. The lobby's never are: the host dialog fixed them.</summary>
    public void SetEditable(bool editable)
    {
        _editable = editable;
        Refresh();
    }

    private void Refresh()
    {
        foreach ((string id, CheckBox box) in _boxes)
            box.Disabled = !_editable || ExpansionRegistry.Find(id)?.IsAvailable != true;
    }
}
