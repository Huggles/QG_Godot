using Godot;

/// <summary>
/// The four-state reaction-skip toggle in a faction's info row: how much this player wants to be
/// asked about reactions for that faction, set ahead of any prompt rather than only from inside one.
///
/// One press cycles <see cref="ReactionSkipPreference.Cycle"/>. The state is carried by a glyph and
/// a colour with the full sentence in the tooltip — deliberately small, because the row is 150x75 and
/// this shares a 25px strip with the army and navy counters.
///
/// Shown only for factions this peer controls. <see cref="FactionInfoRow"/> builds a row for every
/// playable faction (the other five are simply dimmed), and a setting on a faction you do not control
/// would do nothing.
/// </summary>
public partial class ReactionSkipToggleButton : Button
{
    /// <summary>Set by <see cref="FactionInfoRow.LoadUI"/> before the first <see cref="Refresh"/>.</summary>
    public Faction Faction { get; set; }

    /// <summary>
    /// The glyph, in a full-rect child rather than in the Button's own Text.
    ///
    /// A Button is not a Container, so its Control children never contribute to its minimum size —
    /// which is the point. With the text on the Button, the font and outline pushed its minimum to
    /// 31px inside a 25px UnitsStrip, and because that strip carries grow_vertical = BEGIN the strip
    /// grew UPWARDS to fit. This way the button is exactly its custom_minimum_size of 25x25 whatever
    /// glyph is showing, so the row cannot shift.
    /// </summary>
    private Label Glyph => GetNode<Label>("Glyph");

    public override void _Ready()
    {
        FocusMode = FocusModeEnum.None;
        Pressed += OnPressed;
        Refresh();
    }

    public override void _ExitTree()
    {
        Pressed -= OnPressed;
    }

    private void OnPressed()
    {
        ReactionSkipPreference.Cycle(Faction);
        // No Refresh() here: Cycle emits ReactionSkipPreferenceChanged and the row repaints us from
        // that, which is the same path a scope pressed on a live prompt comes down.
    }

    /// <summary>Repaint from the faction's current setting, including one that has just expired.</summary>
    public void Refresh()
    {
        ReactionSkipScope scope = ReactionSkipPreference.ActiveScope(Faction);

        (string glyph, Color colour, string what) = scope switch
        {
            ReactionSkipScope.TURN_STEP =>
                ("S", Colors.Yellow, "skip the rest of this turn step"),
            ReactionSkipScope.ROUND =>
                ("R", Colors.Orange, "skip until my next turn"),
            ReactionSkipScope.UNTIL_ACTIVATABLE =>
                ("A", Colors.LightGreen, "only ask when I can actually react"),
            _ =>
                ("?", Colors.White, "always ask me"),
        };

        Glyph.Text = glyph;
        Glyph.AddThemeColorOverride("font_color", colour);

        // The caveat is the same one the in-prompt buttons carry: a scoped skip only silences the
        // windows that exist to hide information, never a reaction everyone can see you holding.
        TooltipText = $"Reactions: {what}.\n"
                    + "Click to change.\n"
                    + "You are always asked when a card of yours can really react.";
    }
}
