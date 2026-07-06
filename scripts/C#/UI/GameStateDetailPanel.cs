using Godot;
using System.Linq;

/// <summary>
/// Toggleable debug panel showing the full Faction → Deck → Card → Tags hierarchy.
/// Opened/closed via the button in DebugOverlay. Hidden by default.
/// </summary>
public partial class GameStateDetailPanel : PanelContainer
{
    public static GameStateDetailPanel Instance { get; private set; }

    // Tags that are set once at initialisation and never change — not useful for debugging.
    private static readonly System.Collections.Generic.HashSet<Tag> StaticTags = new()
    {
        Tag.LandCountry, Tag.SeaCountry,
    };

    private VBoxContainer _content;

    // ─────────────────────────────────────────────────────────────────────────

    public override void _Ready()
    {
        Instance = this;
        BuildShell();
        Hide();
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Toggle()
    {
        if (Visible) { Hide(); return; }
        Refresh();
        Show();
    }

    public void Refresh()
    {
        ClearContent();

        if (GameFlow.Instance == null)
        {
            _content.AddChild(MakeLabel("Game not started.", new Color(0.55f, 0.55f, 0.55f, 1f), 11));
            return;
        }

        foreach (Faction faction in StaticGameData.PlayableFactions)
            AddFactionSection(faction);
    }

    // ── Content building ──────────────────────────────────────────────────────

    private void AddFactionSection(Faction faction)
    {
        DeckState deck = DeckState.ForFaction(faction);

        VBoxContainer factionBody;
        AddCollapsibleSection(
            _content, faction.ToString(),
            new Color(0.95f, 0.8f, 0.25f, 1f), 12,
            indent: 0, startExpanded: true,
            out factionBody);

        VBoxContainer cardsBody;
        AddCollapsibleSection(
            factionBody, $"Cards ({deck.AllCardIds.Count})",
            new Color(0.55f, 0.85f, 0.55f, 1f), 11,
            indent: 1, startExpanded: false,
            out cardsBody);

        AddCardPile(cardsBody, "Hand",     deck.HandCardStates,      faction);
        AddCardPile(cardsBody, "Deck",     deck.DeckCardStates,      faction);
        AddCardPile(cardsBody, "Discard",  deck.DiscardedCardStates, faction);
        AddCardPile(cardsBody, "Status",   deck.StatusCardStates,    faction);
        AddCardPile(cardsBody, "Response", deck.ResponseCardStates,  faction);

        AddFactionWorldSection(factionBody, faction);

        var spacer = new Control();
        spacer.CustomMinimumSize = new Vector2(0, 4);
        _content.AddChild(spacer);
    }

    private void AddFactionWorldSection(VBoxContainer parent, Faction faction)
    {
        var relevant = CountryState.AllCountryStates
            .Where(cs => cs.Tags.GetTagsForFaction(faction).Any(t => !StaticTags.Contains(t)))
            .OrderBy(cs => cs.Name)
            .ToList();

        if (relevant.Count == 0) return;

        VBoxContainer worldBody;
        AddCollapsibleSection(
            parent, $"World ({relevant.Count})",
            new Color(0.85f, 0.65f, 0.85f, 1f), 11,
            indent: 1, startExpanded: false,
            out worldBody);

        foreach (CountryState cs in relevant)
        {
            string tagStr = string.Join(", ", cs.Tags.GetTagsForFaction(faction).Where(t => !StaticTags.Contains(t)));
            var row = MakeLabel($"  [{cs.Id}] {cs.Name}  [{tagStr}]", new Color(0.82f, 0.82f, 0.82f, 1f), 10);
            row.AutowrapMode = TextServer.AutowrapMode.Off;
            worldBody.AddChild(row);
        }
    }

    private void AddCardPile(VBoxContainer parent, string pileName,
        System.Collections.Generic.List<CardState> cards, Faction faction)
    {
        if (cards.Count == 0) return;

        VBoxContainer pileBody;
        AddCollapsibleSection(
            parent, $"{pileName} ({cards.Count})",
            new Color(0.45f, 0.75f, 1f, 1f), 11,
            indent: 2, startExpanded: false,
            out pileBody);

        foreach (CardState card in cards)
        {
            var tags = card.Tags.GetTagsForFaction(faction);
            string tagStr = tags.Any() ? string.Join(", ", tags) : "—";
            var row = MakeLabel($"  [{card.Id}] {card.CardName}  [{tagStr}]", new Color(0.82f, 0.82f, 0.82f, 1f), 10);
            row.AutowrapMode = TextServer.AutowrapMode.Off;
            pileBody.AddChild(row);
        }
    }

    /// <summary>
    /// Adds a flat toggle button + a body VBoxContainer to <paramref name="parent"/>.
    /// Clicking the button shows/hides the body and updates the ▼/▶ prefix.
    /// </summary>
    private static void AddCollapsibleSection(
        VBoxContainer parent,
        string headerText,
        Color headerColor,
        int fontSize,
        int indent,
        bool startExpanded,
        out VBoxContainer body)
    {
        string pad = new string(' ', indent * 2);
        string arrow = startExpanded ? "▼ " : "▶ ";

        var btn = new Button();
        btn.Text = pad + arrow + headerText;
        btn.Flat = true;
        btn.Alignment = HorizontalAlignment.Left;
        btn.AddThemeColorOverride("font_color", headerColor);
        btn.AddThemeFontSizeOverride("font_size", fontSize);
        btn.AddThemeColorOverride("font_hover_color",   headerColor);
        btn.AddThemeColorOverride("font_pressed_color", headerColor);
        parent.AddChild(btn);

        var capturedBody = new VBoxContainer();
        capturedBody.AddThemeConstantOverride("separation", 1);
        capturedBody.Visible = startExpanded;
        parent.AddChild(capturedBody);

        btn.Pressed += () =>
        {
            capturedBody.Visible = !capturedBody.Visible;
            btn.Text = pad + (capturedBody.Visible ? "▼ " : "▶ ") + headerText;
        };

        body = capturedBody;
    }

    private void ClearContent()
    {
        foreach (Node child in _content.GetChildren())
            child.QueueFree();
    }

    // ── Shell construction ────────────────────────────────────────────────────

    private void BuildShell()
    {
        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.04f, 0.04f, 0.06f, 0.90f);
        style.BorderWidthLeft   = 1;
        style.BorderWidthTop    = 1;
        style.BorderWidthRight  = 1;
        style.BorderWidthBottom = 1;
        style.BorderColor       = new Color(0.32f, 0.32f, 0.32f, 1f);
        style.CornerRadiusTopLeft     = 6;
        style.CornerRadiusTopRight    = 6;
        style.CornerRadiusBottomLeft  = 6;
        style.CornerRadiusBottomRight = 6;
        AddThemeStyleboxOverride("panel", style);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left",   8);
        margin.AddThemeConstantOverride("margin_right",  8);
        margin.AddThemeConstantOverride("margin_top",    6);
        margin.AddThemeConstantOverride("margin_bottom", 6);
        AddChild(margin);

        var outerVBox = new VBoxContainer();
        outerVBox.SizeFlagsVertical = SizeFlags.ExpandFill;
        margin.AddChild(outerVBox);

        // ── Header row ────────────────────────────────────────────────────────
        var header = new HBoxContainer();
        outerVBox.AddChild(header);

        var titleLabel = MakeLabel("── Game State Details ──", new Color(0.7f, 0.85f, 1f, 1f), 11);
        titleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(titleLabel);

        var refreshBtn = new Button();
        refreshBtn.Text = "↺";
        refreshBtn.AddThemeFontSizeOverride("font_size", 12);
        refreshBtn.TooltipText = "Refresh";
        refreshBtn.Pressed += Refresh;
        header.AddChild(refreshBtn);

        // ── Scroll area ───────────────────────────────────────────────────────
        var scroll = new ScrollContainer();
        scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.ShowNever;
        outerVBox.AddChild(scroll);

        _content = new VBoxContainer();
        _content.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _content.AddThemeConstantOverride("separation", 1);
        scroll.AddChild(_content);
    }

    private static Label MakeLabel(string text, Color color, int fontSize)
    {
        var label = new Label();
        label.Text = text;
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeFontSizeOverride("font_size", fontSize);
        return label;
    }
}
