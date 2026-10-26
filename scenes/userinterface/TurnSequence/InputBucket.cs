using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// A bucket at the end of the turn-step strip for every faction with a prompt open: a round tray with
/// that faction's flag in it, in turn order, and nothing at all while nobody is being asked. A bucket
/// drops in only once its prompt has stayed open a moment, so reaction windows answered at once (an
/// auto-pass, a bot) do not make it flicker.
/// </summary>
public partial class InputBucket : Control
{
	private const ulong RevealDelayMs = 250;
	private const float BucketSize = 32f;
	private const float BucketGap = 6f;
	private const float FlagInset = 3f;

	private readonly Dictionary<Faction, ulong> _openedAt = new();
	private readonly Dictionary<Faction, Control> _buckets = new();
	private Timer _revealTimer;
	private Tween _tween;
	private bool _subscribed;

	public override void _Ready()
	{
		_revealTimer = new Timer { OneShot = true };
		_revealTimer.Timeout += Relayout;
		AddChild(_revealTimer);

		// One HUD per PlayerScene; only the local copy is ever on screen.
		if (!SessionIdentity.IsLocalAuthority(this)) return;
		EventBus.Instance.OpenPromptsChanged += OnOpenPromptsChanged;
		_subscribed = true;
		OnOpenPromptsChanged();
	}

	public override void _ExitTree()
	{
		if (!_subscribed || EventBus.Instance == null) return;
		_subscribed = false;
		EventBus.Instance.OpenPromptsChanged -= OnOpenPromptsChanged;
	}

	private void OnOpenPromptsChanged()
	{
		IReadOnlyCollection<Faction> open = InputRequest.OpenPromptFactions;
		foreach (Faction closed in _openedAt.Keys.Except(open).ToList()) _openedAt.Remove(closed);
		foreach (Faction faction in open) _openedAt.TryAdd(faction, Time.GetTicksMsec());
		Relayout();
	}

	private void Relayout()
	{
		ulong now = Time.GetTicksMsec();
		List<Faction> shown = _openedAt
			.Where(entry => now - entry.Value >= RevealDelayMs && FlagFor(entry.Key) != null)
			.Select(entry => entry.Key)
			.OrderBy(faction => (int)faction)
			.ToList();

		foreach (Faction gone in _buckets.Keys.Except(shown).ToList()) DropBucket(gone);

		// Wake for the earliest prompt still inside its delay; any later ones re-arm it from there.
		ulong[] waiting = _openedAt.Values.Where(openedAt => now - openedAt < RevealDelayMs).ToArray();
		if (waiting.Length > 0) _revealTimer.Start((waiting.Min() + RevealDelayMs - now) / 1000.0);

		_tween?.Kill();
		if (shown.Count == 0) return;
		_tween = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		double duration = GameSettings.ShowHideSeconds;
		for (int i = 0; i < shown.Count; i++)
		{
			Vector2 position = new(i * (BucketSize + BucketGap), 0);
			if (!_buckets.TryGetValue(shown[i], out Control bucket)) bucket = AddBucket(shown[i], position);

			_tween.TweenProperty(bucket, "position", position, duration);
			_tween.TweenProperty(bucket, "scale", Vector2.One, duration);
			_tween.TweenProperty(bucket, "modulate:a", 1f, duration);
		}
	}

	private Control AddBucket(Faction faction, Vector2 position)
	{
		Panel bucket = new()
		{
			Position = position,
			Size = Vector2.One * BucketSize,
			PivotOffset = Vector2.One * BucketSize / 2,
			Scale = Vector2.One * 0.4f,
			Modulate = new Color(1, 1, 1, 0),
			MouseFilter = MouseFilterEnum.Pass,
			TooltipText = faction.WithPlayer(),
		};
		bucket.AddThemeStyleboxOverride("panel", RimStyle());
		bucket.AddChild(new TextureRect
		{
			Texture = FlagFor(faction),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = TextureFilterEnum.LinearWithMipmaps,
			MouseFilter = MouseFilterEnum.Ignore,
			Position = Vector2.One * FlagInset,
			Size = Vector2.One * (BucketSize - 2 * FlagInset),
		});
		AddChild(bucket);
		_buckets[faction] = bucket;
		return bucket;
	}

	/// <summary>Faded out on a tween of its own, so the next relayout's Kill cannot strand it half gone.</summary>
	private void DropBucket(Faction faction)
	{
		Control bucket = _buckets[faction];
		_buckets.Remove(faction);
		Tween fade = bucket.CreateTween();
		fade.TweenProperty(bucket, "modulate:a", 0f, GameSettings.ShowHideSeconds);
		fade.TweenCallback(Callable.From(bucket.QueueFree));
	}

	private static StyleBoxFlat RimStyle()
	{
		StyleBoxFlat style = new()
		{
			BgColor = new Color(0.1f, 0.1f, 0.1f, 0.6f),
			BorderColor = new Color(1f, 1f, 1f, 0.35f),
			CornerDetail = 16,
		};
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll((int)(BucketSize / 2));
		return style;
	}

	private static Texture2D FlagFor(Faction faction)
		=> StaticGameData.FactionDataMap.TryGetValue(faction, out FactionData data) ? data.FlagCircleTexture : null;
}
