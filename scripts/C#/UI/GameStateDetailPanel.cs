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
    private EventBus.GameStateRecalculatedEventHandler _onRecalculated;
    private readonly System.Collections.Generic.HashSet<string> _expandedSections = new();

    // ─────────────────────────────────────────────────────────────────────────

    public override void _Ready()
    {
        Instance = this;
        BuildShell();
        Hide();

        _onRecalculated = () => { if (Visible) CallDeferred(MethodName.Refresh); };
        EventBus.Instance.GameStateRecalculated += _onRecalculated;
    }

    public override void _ExitTree()
    {
        if (EventBus.Instance != null && _onRecalculated != null)
            EventBus.Instance.GameStateRecalculated -= _onRecalculated;
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

    private void AddUnitsSection()
    {
        var allUnits = UnitState.AllUnitStates
            .Where(u => u.IsDeployedToCountry)
            .ToList();

        int total = allUnits.Count;

        VBoxContainer unitsBody;
        AddCollapsibleSection(
            _content, $"Units ({total})",
            new Color(0.85f, 0.75f, 0.45f, 1f), 12,
            indent: 0, startExpanded: true, sectionKey: "Units",
            out unitsBody);

        if (total == 0)
        {
            unitsBody.AddChild(MakeLabel("  no deployed units", new Color(0.55f, 0.55f, 0.55f, 1f), 10));
            return;
        }

        var byFaction = allUnits
            .GroupBy(u => u.Faction)
            .OrderBy(g => g.Key.ToString());

        foreach (var group in byFaction)
        {
            Faction faction = group.Key;
            var units = group.OrderBy(u => u.Type.ToString()).ToList();

            VBoxContainer factionBody;
            AddCollapsibleSection(
                unitsBody, $"{faction} ({units.Count})",
                new Color(0.95f, 0.8f, 0.25f, 1f), 11,
                indent: 1, startExpanded: true, sectionKey: $"Units/{faction}",
                out factionBody);

            foreach (var unit in units)
            {
                var tags = unit.Tags.GetTagsForFaction(unit.Faction);
                string tagStr = tags.Any() ? string.Join(", ", tags) : "—";
                string countryName = unit.CountryState?.Name ?? "?";

                var row = MakeLabel(
                    $"    [{unit.Id}] {unit.Type} @ {countryName}  [{tagStr}]",
                    new Color(0.82f, 0.82f, 0.82f, 1f), 10);
                row.AutowrapMode = TextServer.AutowrapMode.Off;
                factionBody.AddChild(row);
            }
        }
    }

    private void AddFactionSection(Faction faction)
    {
        DeckState deck = DeckState.ForFaction(faction);
        string fk = faction.ToString();

        VBoxContainer factionBody;
        AddCollapsibleSection(
            _content, faction.ToString(),
            new Color(0.95f, 0.8f, 0.25f, 1f), 12,
            indent: 0, startExpanded: true, sectionKey: fk,
            out factionBody);

        VBoxContainer cardsBody;
        AddCollapsibleSection(
            factionBody, $"Cards ({deck.AllCardIds.Count})",
            new Color(0.55f, 0.85f, 0.55f, 1f), 11,
            indent: 1, startExpanded: false, sectionKey: fk + "/Cards",
            out cardsBody);

        AddCardPile(cardsBody, "Hand",     deck.HandCardStates,      faction, fk + "/Cards");
        AddCardPile(cardsBody, "Deck",     deck.DeckCardStates,      faction, fk + "/Cards");
        AddCardPile(cardsBody, "Discard",  deck.DiscardedCardStates, faction, fk + "/Cards");
        AddCardPile(cardsBody, "Status",   deck.StatusCardStates,    faction, fk + "/Cards");
        AddCardPile(cardsBody, "Response", deck.ResponseCardStates,  faction, fk + "/Cards");

        AddFactionWorldSection(factionBody, faction, fk);
        AddFactionUnitsSection(factionBody, faction, fk);

        var spacer = new Control();
        spacer.CustomMinimumSize = new Vector2(0, 4);
        _content.AddChild(spacer);
    }

    private void AddFactionUnitsSection(VBoxContainer parent, Faction faction, string factionKey)
    {
        var units = UnitState.AllUnitStates
            .Where(u => u.Faction == faction && u.IsDeployedToCountry)
            .OrderBy(u => u.Type.ToString())
            .ToList();

        VBoxContainer unitsBody;
        AddCollapsibleSection(
            parent, $"Units ({units.Count})",
            new Color(0.85f, 0.75f, 0.45f, 1f), 11,
            indent: 1, startExpanded: false, sectionKey: factionKey + "/Units",
            out unitsBody);

        if (units.Count == 0)
        {
            unitsBody.AddChild(MakeLabel("  no deployed units", new Color(0.55f, 0.55f, 0.55f, 1f), 10));
            return;
        }

        foreach (var unit in units)
        {
            var tags = unit.Tags.GetTagsForFaction(unit.Faction);
            string tagStr = tags.Any() ? string.Join(", ", tags) : "—";
            string countryName = unit.CountryState?.Name ?? "?";

            var row = MakeLabel(
                $"  [{unit.Id}] {unit.Type} @ {countryName}  [{tagStr}]",
                new Color(0.82f, 0.82f, 0.82f, 1f), 10);
            row.AutowrapMode = TextServer.AutowrapMode.Off;
            unitsBody.AddChild(row);
        }
    }

    private void AddFactionWorldSection(VBoxContainer parent, Faction faction, string factionKey)
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
            indent: 1, startExpanded: false, sectionKey: factionKey + "/World",
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
        System.Collections.Generic.List<CardState> cards, Faction faction, string parentKey)
    {
        if (cards.Count == 0) return;
        string pileKey = parentKey + "/" + pileName;

        VBoxContainer pileBody;
        AddCollapsibleSection(
            parent, $"{pileName} ({cards.Count})",
            new Color(0.45f, 0.75f, 1f, 1f), 11,
            indent: 2, startExpanded: false, sectionKey: pileKey,
            out pileBody);

        foreach (CardState card in cards)
        {
            var tags = card.Tags.GetTagsForFaction(faction);
            string tagStr = tags.Any() ? string.Join(", ", tags) : "—";

            VBoxContainer cardBody;
            AddCollapsibleSection(
                pileBody, $"[{card.Id}] {card.CardName}  [{tagStr}]",
                new Color(0.82f, 0.82f, 0.82f, 1f), 10,
                indent: 3, startExpanded: false, sectionKey: pileKey + "/" + card.Id,
                out cardBody);

            AddCardConditions(cardBody, card);
        }
    }

    private static void AddCardConditions(VBoxContainer parent, CardState card)
    {
        var logic = card.CardLogic;
        if (logic == null) return;

        var conditions = logic._conditions;
        if (conditions == null || conditions.Count == 0)
        {
            parent.AddChild(MakeLabel("    —", new Color(0.5f, 0.5f, 0.5f, 1f), 10));
            return;
        }

        foreach (var condition in conditions)
        {
            bool met;
            try { met = condition.MeetCondition(); }
            catch { met = false; }

            string symbol = met ? "✓" : "✗";
            var color = met ? new Color(0.35f, 0.85f, 0.35f, 1f) : new Color(0.85f, 0.35f, 0.35f, 1f);
            var label = MakeLabel($"    {symbol} {ConditionName(condition)}", color, 10);
            label.AutowrapMode = TextServer.AutowrapMode.Off;
            parent.AddChild(label);
        }
    }

    private static string ConditionName(Condition condition) =>
        condition is Condition.Not not
            ? $"Not({ConditionName(not.Inner)})"
            : condition.GetType().Name;    /// <summary>
    /// Adds a flat toggle button + a body VBoxContainer to <paramref name="parent"/>.
    /// Clicking the button shows/hides the body and updates the ▼/▶ prefix.
    /// </summary>
    private void AddCollapsibleSection(
        VBoxContainer parent,
        string headerText,
        Color headerColor,
        int fontSize,
        int indent,
        bool startExpanded,
        string sectionKey,
        out VBoxContainer body)
    {
        string pad = new string(' ', indent * 2);

        bool expanded;
        if (_expandedSections.Contains(sectionKey))
            expanded = true;
        else if (startExpanded)
        {
            _expandedSections.Add(sectionKey);
            expanded = true;
        }
        else
            expanded = false;

        var btn = new Button();
        btn.Text = pad + (expanded ? "▼ " : "▶ ") + headerText;
        btn.Flat = true;
        btn.Alignment = HorizontalAlignment.Left;
        btn.AddThemeColorOverride("font_color", headerColor);
        btn.AddThemeFontSizeOverride("font_size", fontSize);
        btn.AddThemeColorOverride("font_hover_color",   headerColor);
        btn.AddThemeColorOverride("font_pressed_color", headerColor);
        parent.AddChild(btn);

        var capturedBody = new VBoxContainer();
        capturedBody.AddThemeConstantOverride("separation", 1);
        capturedBody.Visible = expanded;
        parent.AddChild(capturedBody);

        btn.Pressed += () =>
        {
            capturedBody.Visible = !capturedBody.Visible;
            if (capturedBody.Visible)
                _expandedSections.Add(sectionKey);
            else
                _expandedSections.Remove(sectionKey);
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
