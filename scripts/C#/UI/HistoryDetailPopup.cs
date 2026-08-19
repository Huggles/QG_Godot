using Godot;

/// <summary>
/// The hover detail for one game history badge: the card that triggered it, drawn as a real card,
/// plus the message summary.
///
/// One instance owned by the Interface CanvasLayer rather than one popup per badge. GameHistoryList
/// sets clip_contents, so anything parented under a badge is clipped by the 48px strip; and a strip
/// full of badges would mean a CardScene each, every one of them loading a card texture. Same
/// singleton shape as <see cref="TriggerContextDisplay"/>, for the same reasons.
/// </summary>
public partial class HistoryDetailPopup : Control, LoadableUI
{
    public static HistoryDetailPopup Current;

    /// <summary>Gap between the hovered badge and the popup's near edge.</summary>
    private const float GapFromBadge = 8f;
    /// <summary>How close the popup may get to the edge of the screen.</summary>
    private const float ScreenMargin = 10f;

    /// <summary>
    /// Text column width when a card is shown — matched to the card so the two edges line up.
    /// </summary>
    private const float TextWidthWithCard = 250f;
    /// <summary>
    /// Text column width for a text-only entry. The whole popup is nothing but this line, so it wraps
    /// narrower and the panel shrinks to fit rather than leaving a card's worth of empty space.
    /// </summary>
    private const float TextWidthAlone = 170f;

    private PanelContainer DetailPanel => GetNode<PanelContainer>("%HistoryDetailPanel");
    private CardScene      CardSceneNode => GetNode<CardScene>("%HistoryCard");
    private RichTextLabel  SummaryLabel => GetNode<RichTextLabel>("%HistorySummary");

    /// <summary>
    /// Which badge the popup currently belongs to. The token that makes the hover hand-off between
    /// two adjacent badges order-independent: Godot does not guarantee whether exited(A) or
    /// entered(B) fires first inside one motion event, and <see cref="HideFor"/> being a no-op
    /// unless the caller still owns the popup is correct either way.
    /// </summary>
    private Control owner;

    public override void _Ready()
    {
        if (GetMultiplayerAuthority() == Multiplayer.GetUniqueId())
        {
            Current = this;
            LoadUI();
        }
    }

    public void LoadUI()
    {
        CardSceneNode.TriggersEmphasis(false);   // a history hover must not drive the hand's card preview
        CardSceneNode.SetClickable(false);
        CardSceneNode.SetActivatable(true);      // clears the red "cannot use this" scrim
        CardSceneNode.SetMousePassthrough(true); // the popup floats over the board; it must not eat clicks
        DetailPanel.Visible = false;
        SetProcess(false);
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        if (Current == this) Current = null;
    }

    /// <summary>Open the popup for <paramref name="badge"/>, taking ownership from whoever had it.</summary>
    public void ShowFor(Control badge, GameHistoryEntry entry)
    {
        if (DetailPanel == null) return;

        owner = badge;

        // CardState.ForId returns null for an id that is not in game state, and CardFace.ForCard
        // would NRE on it. ShowCard routes through CardFace.ForCard, which branches on
        // IsFaceVisibleToLocalPlayer — so a face-down Response renders as the faction card back for
        // free, and because that is evaluated per hover, a card revealed later starts showing its
        // face in the history too.
        CardState card = entry.SourceCardId > -1 ? CardState.ForId(entry.SourceCardId) : null;
        if (card != null)
        {
            CardSceneNode.Visible = true;
            CardSceneNode.ShowCard(entry.SourceCardId);
        }
        else if (entry.BulletinLabel != null)
        {
            CardSceneNode.Visible = true;
            CardSceneNode.ShowFace(CardFace.Bulletin(entry.BulletinLabel, entry.BulletinText));
        }
        else
        {
            // Containers skip hidden children, so this genuinely collapses the popup down to its text.
            CardSceneNode.Visible = false;
        }

        // Drives both the wrap width and, through it, the whole panel's width — the PanelContainer has
        // no minimum of its own, so a text-only entry collapses to just this column plus the margins.
        SummaryLabel.CustomMinimumSize = new Vector2(
            CardSceneNode.Visible ? TextWidthWithCard : TextWidthAlone, 0);

        // The sequence number is the same one on the badge, so a player can tie the popup back to the
        // entry they are pointing at. The stream id is debug-only: it is not what the badge shows.
        SummaryLabel.Text = GameSettings.ShowDebugMenu
            ? $"[color=#888888]{entry.Sequence}.[/color] {entry.Summary}\n[color=#888888]stream #{entry.MessageId}[/color]"
            : $"[color=#888888]{entry.Sequence}.[/color] {entry.Summary}";

        DetailPanel.Visible = true;
        Reposition(badge);
        SetProcess(true);
    }

    /// <summary>Release the popup, but only if <paramref name="badge"/> still owns it.</summary>
    public void HideFor(Control badge)
    {
        if (owner != badge) return;
        Release();
    }

    /// <summary>
    /// Only runs while the popup is open, and does two things.
    ///
    /// It releases the popup on the one way a hover can end that the engine does not report as a
    /// MouseExited: a modal opening on top of a stationary cursor. (A badge trimmed out of the window
    /// or clipped away is caught here too, though GameHistoryItem._ExitTree already handles those.)
    ///
    /// And it re-places the popup every frame, which is not belt-and-braces. The strip is
    /// bottom-aligned, so every new history entry shifts every existing badge up by a row — a popup
    /// placed once would end up pointing at the wrong badge while the cursor never moved. Repositioning
    /// continuously also absorbs the frame of lag in RichTextLabel's fit_content height, which is
    /// computed from a width the label only has after a layout pass.
    ///
    /// One rect test and a handful of float ops a frame, and only while something is shown.
    /// </summary>
    public override void _Process(double delta)
    {
        if (owner == null || !IsInstanceValid(owner) || !owner.IsVisibleInTree()
            || !owner.GetGlobalRect().HasPoint(owner.GetGlobalMousePosition()))
        {
            Release();
            return;
        }
        Reposition(owner);
    }

    private void Release()
    {
        owner = null;
        SetProcess(false);
        if (DetailPanel != null) DetailPanel.Visible = false;
    }

    /// <summary>
    /// Place the popup to the left of the badge — the strip hugs the right screen edge — vertically
    /// centred on it and clamped to stay on screen.
    ///
    /// Note the coordinate space: GetGlobalRect()/GlobalPosition on a Control are in the owning
    /// CanvasLayer's canvas space, not screen space. Badge and popup both live in the Interface
    /// layer, so they compare directly. Clamping against this root's own rect (a full-rect control
    /// in that same canvas) rather than GetViewportRect() keeps all the arithmetic in one space —
    /// the viewport rect only coincides with canvas space while the layer transform is identity.
    /// </summary>
    private void Reposition(Control badge)
    {
        // GetCombinedMinimumSize rather than DetailPanel.Size: Size is still last layout's value in
        // the frame the summary text changed, which would misplace the popup by the size delta.
        Vector2 size = DetailPanel.GetCombinedMinimumSize();

        // Assigned, not left to the layout: the panel is freely positioned rather than in a container,
        // so Godot only ever clamps its size *up* to the minimum and it would otherwise keep whatever
        // the scene's offsets gave it — a card-sized box around a single line of text.
        DetailPanel.Size = size;

        Rect2 anchor = badge.GetGlobalRect();
        Rect2 bounds = GetGlobalRect();

        float x = anchor.Position.X - GapFromBadge - size.X;
        float y = anchor.Position.Y + anchor.Size.Y * 0.5f - size.Y * 0.5f;

        x = Mathf.Max(bounds.Position.X + ScreenMargin, x);
        y = Mathf.Clamp(
            y,
            bounds.Position.Y + ScreenMargin,
            Mathf.Max(bounds.Position.Y + ScreenMargin, bounds.End.Y - size.Y - ScreenMargin));

        DetailPanel.GlobalPosition = new Vector2(x, y);
    }
}
