using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// The opening fly-over: once the loading cover lifts, the camera visits each faction's capital in turn
/// order and names the faction and who plays it. Local presentation only, built in code in the menu
/// style (see STYLE_GUIDE.md). Escape skips it. The host holds the first prompt until every peer is done
/// (<see cref="FactionIntroBarrier"/>).
/// </summary>
public partial class FactionIntro : CanvasLayer
{
    private const float ZoomFactor = 2.5f;          // relative to the framed board
    private static readonly Vector2 CapitalAt = new(0.30f, 0.54f); // where the capital lands on screen
    private const double FlightSeconds = 1.5;
    private const double HoldSeconds = 3.4;
    private const double FadeSeconds = 0.35;
    private const float BarHeight = 96f;

    private static readonly Color Brass = new(0.851f, 0.678f, 0.361f);
    private static readonly Color Spark = new(1f, 0.93f, 0.74f);
    private static readonly Color Cream = new(0.929f, 0.898f, 0.82f);
    private static readonly Color CreamHover = new(0.96f, 0.82f, 0.55f);
    private static readonly Color Muted = new(0.69f, 0.66f, 0.58f);
    private static readonly Color Scrim = new(0.02f, 0.024f, 0.018f);

    private Camera2D _camera;
    private CanvasLayer _hud;
    private readonly List<(CanvasItem item, Color modulate)> _hudItems = new();
    private Vector2 _homePosition, _homeZoom;
    private bool _skipped;
    private Tween _cameraTween, _beatTween;

    private ColorRect _barTop, _barBottom;
    private Control _shade;
    private VBoxContainer _card;
    private Label _kicker, _faction, _player, _aiTag;
    private ColorRect _factionBar;
    private Control _ornament, _who;
    private CountryScene _focus;
    private Control _pulse;
    private float _pulsePhase;

    /// <summary>Plays the intro on this peer and returns when it is over; a no-op without a screen.</summary>
    public static async Task PlayAsync()
    {
        InputManager input = InputManager.Current;
        PlayerScene player = PlayerScene.Current;
        if (GameContext.IsHeadless || input?.Camera == null || player == null) return;

        FactionIntro intro = new() { Layer = 60, Name = "FactionIntro" };
        player.AddChild(intro);
        try { await intro.Run(input); }
        finally { if (IsInstanceValid(intro)) intro.Finish(input); }
    }

    private async Task Run(InputManager input)
    {
        _camera = input.Camera;
        _homePosition = _camera.GlobalPosition;
        _homeZoom = _camera.Zoom;
        input.CameraLocked = true;
        HideHud();
        Build();

        // Let the loading cover fade over the full board before the first flight.
        if (!await Wait(GameSettings.ShowHideSeconds + 0.3)) return;
        FadeTo(_shade, 1.0, 0.8);

        foreach (Faction faction in StaticGameData.PlayableFactions)
        {
            if (_skipped || !await ShowFaction(faction)) break;
        }
        await Outro();
    }

    private async Task<bool> ShowFaction(Faction faction)
    {
        FactionData data = StaticGameData.FactionDataMap.GetValueOrDefault(faction);
        CountryScene capital = data?.HomeSpaceCountryState?.CountryScene;
        if (capital == null) return true;

        _focus = null;
        Fill(faction, data);
        Vector2 view = GetViewport().GetVisibleRect().Size;
        Vector2 zoom = _homeZoom * ZoomFactor;
        Vector2 star = StarPoint(capital);
        Vector2 target = ClampToMap(star + (new Vector2(0.5f, 0.5f) - CapitalAt) * view / zoom, view / (2 * zoom));
        _cameraTween = MoveCamera(target, zoom, FlightSeconds);
        if (!await Wait(FlightSeconds)) return false;

        // Followed every frame from here (see _Process): the clamp and the tail of the flight move it.
        _focus = capital;
        Place(StarOnScreen(capital), view);
        RevealCard();
        if (!await Wait(HoldSeconds)) return false;

        _beatTween?.Kill();
        Tween fade = CreateTween().SetParallel();
        foreach (CanvasItem item in new CanvasItem[] { _card, _pulse })
            fade.TweenProperty(item, "modulate:a", 0.0, FadeSeconds);
        return await Wait(FadeSeconds);
    }

    private async Task Outro()
    {
        _focus = null;
        _beatTween?.Kill();
        _cameraTween?.Kill();
        Tween fade = CreateTween().SetParallel();
        foreach (CanvasItem item in new CanvasItem[] { _card, _pulse, _shade })
            fade.TweenProperty(item, "modulate:a", 0.0, FadeSeconds);
        fade.TweenProperty(_barTop, "position:y", -BarHeight, 0.8).SetTrans(Godot.Tween.TransitionType.Cubic);
        fade.TweenProperty(_barBottom, "position:y", GetViewport().GetVisibleRect().Size.Y, 0.8).SetTrans(Godot.Tween.TransitionType.Cubic);
        _cameraTween = MoveCamera(_homePosition, _homeZoom, 1.2);
        ShowHud(1.0);
        _skipped = false; // the pull-back always plays out, even after Escape
        await Wait(1.2);
    }

    /// <summary>Puts the HUD and camera back whatever happened; safe to call twice.</summary>
    private void Finish(InputManager input)
    {
        if (IsInstanceValid(input))
        {
            input.CameraLocked = false;
            if (_camera != null && IsInstanceValid(_camera)) { _camera.GlobalPosition = _homePosition; _camera.Zoom = _homeZoom; }
        }
        ShowHud(0);
        QueueFree();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) return;
        GetViewport().SetInputAsHandled(); // or the in-game menu opens behind the intro
        _skipped = true;
    }

    public override void _Process(double delta)
    {
        _pulsePhase = (_pulsePhase + (float)delta / 2.4f) % 1f;
        _pulse?.QueueRedraw();
        if (_focus != null && IsInstanceValid(_focus)) Place(StarOnScreen(_focus), GetViewport().GetVisibleRect().Size);
    }

    // ── HUD ────────────────────────────────────────────────────────────────

    private void HideHud()
    {
        _hud = PlayerScene.Current?.GetNodeOrNull<CanvasLayer>("Interface");
        if (_hud == null) return;
        foreach (Node child in _hud.GetChildren())
            if (child is CanvasItem item) _hudItems.Add((item, item.Modulate));
        _hud.Visible = false;
    }

    private void ShowHud(double seconds)
    {
        if (_hud == null || !IsInstanceValid(_hud) || _hud.Visible) return;
        _hud.Visible = true;
        foreach ((CanvasItem item, Color modulate) in _hudItems)
        {
            if (!IsInstanceValid(item)) continue;
            if (seconds <= 0) { item.Modulate = modulate; continue; }
            item.Modulate = modulate with { A = 0 };
            item.CreateTween().TweenProperty(item, "modulate:a", modulate.A, seconds);
        }
    }

    // ── Camera and timing ─────────────────────────────────────────────────

    private Tween MoveCamera(Vector2 position, Vector2 zoom, double seconds)
    {
        _cameraTween?.Kill();
        Tween tween = _camera.CreateTween().SetParallel();
        tween.TweenProperty(_camera, "global_position", position, seconds).SetTrans(Godot.Tween.TransitionType.Sine).SetEase(Godot.Tween.EaseType.InOut);
        tween.TweenProperty(_camera, "zoom", zoom, seconds).SetTrans(Godot.Tween.TransitionType.Sine).SetEase(Godot.Tween.EaseType.InOut);
        return tween;
    }

    /// <summary>The capital's supply star, or the territory's middle when it has none showing.</summary>
    private static Vector2 StarPoint(CountryScene capital)
    {
        TextureRect star = capital.SupplyStarSprite;
        return star.Visible ? star.GetGlobalTransform() * (star.Size / 2) : capital.GlobalCenter;
    }

    private static Vector2 StarOnScreen(CountryScene capital)
    {
        TextureRect star = capital.SupplyStarSprite;
        return star.Visible
            ? star.GetGlobalTransformWithCanvas() * (star.Size / 2)
            : capital.GetGlobalTransformWithCanvas() * (capital.Size / 2);
    }

    /// <summary>Keeps the view inside the map, clear of the riveted frame and what lies past it.</summary>
    private static Vector2 ClampToMap(Vector2 center, Vector2 halfExtent)
    {
        Rect2? frame = NodeUtilities.Instance?.BoardFrameBounds ?? NodeUtilities.Instance?.BoardBounds;
        if (frame == null) return center;
        Rect2 map = frame.Value.GrowIndividual(
            -frame.Value.Size.X * 0.04f, -frame.Value.Size.Y * 0.07f, -frame.Value.Size.X * 0.04f, -frame.Value.Size.Y * 0.08f);
        Vector2 min = map.Position + halfExtent, max = map.End - halfExtent;
        return new Vector2(
            min.X <= max.X ? Mathf.Clamp(center.X, min.X, max.X) : map.GetCenter().X,
            min.Y <= max.Y ? Mathf.Clamp(center.Y, min.Y, max.Y) : map.GetCenter().Y);
    }

    /// <summary>False when the intro was skipped or torn down (the game quit) while waiting.</summary>
    private async Task<bool> Wait(double seconds)
    {
        ulong end = Time.GetTicksMsec() + (ulong)(seconds * 1000);
        while (Time.GetTicksMsec() < end)
        {
            if (_skipped || !IsInsideTree()) return false;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        return !_skipped && IsInsideTree();
    }

    private void FadeTo(CanvasItem target, double alpha, double seconds)
        => CreateTween().TweenProperty(target, "modulate:a", alpha, seconds);

    // ── Layout ────────────────────────────────────────────────────────────

    private void Build()
    {
        Vector2 view = GetViewport().GetVisibleRect().Size;

        // Darkens the right side so the text reads over any part of the map.
        _shade = new Control { Size = view, MouseFilter = Control.MouseFilterEnum.Ignore, Modulate = Colors.White with { A = 0 } };
        _shade.Draw += DrawShade;
        AddChild(_shade);

        _pulse = new Control { Size = new Vector2(140, 140), MouseFilter = Control.MouseFilterEnum.Ignore, Modulate = Colors.White with { A = 0 } };
        _pulse.Draw += DrawPulse;
        AddChild(_pulse);


        BuildCard();

        _barTop = Bar(new Vector2(0, 0), view.X);
        _barBottom = Bar(new Vector2(0, view.Y - BarHeight), view.X);
        Label skip = Caps("Esc   skip intro", 15, Muted);
        skip.Position = new Vector2(view.X - 260, (BarHeight - 22) / 2);
        skip.Size = new Vector2(220, 22);
        skip.HorizontalAlignment = HorizontalAlignment.Right;
        _barBottom.AddChild(skip);

        _card.Modulate = Colors.White with { A = 0 };
    }

    private ColorRect Bar(Vector2 position, float width)
    {
        ColorRect bar = new() { Color = Scrim, Position = position, Size = new Vector2(width, BarHeight), MouseFilter = Control.MouseFilterEnum.Ignore };
        AddChild(bar);
        return bar;
    }

    private void BuildCard()
    {
        _card = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _card.AddThemeConstantOverride("separation", 4);
        AddChild(_card);

        _kicker = Caps("", 22, Brass);
        _faction = Display("", 150, Cream);
        _card.AddChild(_kicker);
        _card.AddChild(_faction);

        // The main-menu ornament, with the faction colour on its bar.
        HBoxContainer ornament = new() { MouseFilter = Control.MouseFilterEnum.Ignore };
        ornament.AddThemeConstantOverride("separation", 14);
        _factionBar = new ColorRect { CustomMinimumSize = new Vector2(88, 7), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        Label star = Caps("★", 22, Brass);
        ColorRect hair = new() { Color = Brass with { A = 0.45f }, CustomMinimumSize = new Vector2(300, 1), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        ornament.AddChild(_factionBar);
        ornament.AddChild(star);
        ornament.AddChild(hair);
        _ornament = ornament;
        _card.AddChild(ornament);

        HBoxContainer who = new() { MouseFilter = Control.MouseFilterEnum.Ignore };
        who.AddThemeConstantOverride("separation", 18);
        _player = Display("", 78, CreamHover);
        _aiTag = Caps("AI", 17, Muted);
        _aiTag.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        who.AddChild(_player);
        who.AddChild(_aiTag);
        _who = who;
        _card.AddChild(who);
    }

    private void Fill(Faction faction, FactionData data)
    {
        _kicker.Text = data.Team ?? "";
        _faction.Text = faction.Label();
        _factionBar.Color = data.FactionColor;

        bool ai = PlayerFactionRegistry.IsFactionAi(faction);
        _player.Text = ai ? "Computer" : PlayerFactionRegistry.GetDisplayNameForFaction(faction) ?? "You";
        _aiTag.Visible = ai;
    }

    /// <summary>The card sits to the right of the capital at <paramref name="at"/>, kept on screen.</summary>
    private void Place(Vector2 at, Vector2 view)
    {
        _pulse.Position = at - _pulse.Size / 2 - new Vector2(3, 3); // optical centre of the star art
        float gap = view.X * 0.075f;

        _card.ResetSize();
        Vector2 size = _card.GetCombinedMinimumSize();
        float x = Mathf.Min(at.X + gap, view.X - size.X - 48);
        float y = Mathf.Clamp(at.Y - size.Y / 2, BarHeight + 16, view.Y - BarHeight - 16 - size.Y);
        _card.Position = new Vector2(x, y);
    }

    private void RevealCard()
    {
        _beatTween?.Kill();
        foreach (CanvasItem item in new CanvasItem[] { _kicker, _faction, _ornament, _who }) item.Modulate = Colors.White with { A = 0 };
        _card.Modulate = Colors.White;
        _pulse.Modulate = Colors.White;
        _pulsePhase = 0;

        Tween t = CreateTween().SetParallel();
        Step(t, _kicker, 0.1);
        Step(t, _faction, 0.25);
        Step(t, _ornament, 0.55);
        Step(t, _who, 0.8);
        _beatTween = t;
    }

    private static void Step(Tween t, Control item, double delay)
    {
        t.TweenProperty(item, "modulate:a", 1.0, 0.45).SetDelay(delay);
    }

    // A white ring that keeps swelling out from the capital's star and fading.
    private void DrawPulse()
    {
        _pulse.DrawArc(_pulse.Size / 2, 42f + 28f * _pulsePhase, 0, Mathf.Tau, 64, Spark with { A = 0.7f * (1f - _pulsePhase) }, 2, true);
    }

    // Horizontal bands from clear to near-black scrim; vertex colours do the gradient.
    private void DrawShade()
    {
        float[] at = { 0f, 0.22f, 0.46f, 0.78f, 1f };
        float[] alpha = { 0f, 0f, 0.42f, 0.70f, 0.78f };
        Vector2 size = _shade.Size;
        for (int i = 0; i < at.Length - 1; i++)
        {
            Color a = Scrim with { A = alpha[i] }, b = Scrim with { A = alpha[i + 1] };
            _shade.DrawPolygon(
                new[] { new Vector2(at[i] * size.X, 0), new Vector2(at[i + 1] * size.X, 0), new Vector2(at[i + 1] * size.X, size.Y), new Vector2(at[i] * size.X, size.Y) },
                new[] { a, b, b, a });
        }
    }

    // ── Type ──────────────────────────────────────────────────────────────

    private static readonly FontVariation DisplayFont = GD.Load<FontVariation>("res://scenes/menu/theme/MenuFontDisplay.tres");
    private static readonly FontVariation CapsFont = GD.Load<FontVariation>("res://scenes/menu/theme/MenuFontCaps.tres");

    private static Label Display(string text, int size, Color color)
    {
        Label label = Styled(text, DisplayFont, size, color);
        label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.6f));
        label.AddThemeConstantOverride("shadow_offset_x", 0);
        label.AddThemeConstantOverride("shadow_offset_y", 4);
        label.AddThemeConstantOverride("shadow_outline_size", 12);
        return label;
    }

    private static Label Caps(string text, int size, Color color) => Styled(text, CapsFont, size, color);

    private static Label Styled(string text, Font font, int size, Color color)
    {
        Label label = new() { Text = text, Uppercase = true, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontOverride("font", font);
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }
}
