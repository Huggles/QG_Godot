using Godot;
using System.Collections.Generic;

/// <summary>
/// The tray beside the history strip where a timed-out draw or discard modal lingers, shrunk, before it
/// folds into the newest history badge. Created by <see cref="ModalStack"/> as its sibling.
///
/// Takes the modal node itself rather than a copy: the modal has already resolved, so the game moves on
/// while this plays out, and reparenting it out of the modal row frees its slot at once.
/// </summary>
public partial class RevealDock : Control
{
	public static RevealDock Current { get; private set; }

	private const int MaxEntries = 3;
	private const int LingerMilliseconds = 6000;
	private const float MaxWidth = 300f;
	private const float MaxHeight = 170f;
	private const float StripGap = 8f;
	private const float EntryGap = 6f;
	private const float HoverZoom = 2.2f;
	private const float OlderAlpha = 0.75f;
	private const double HoverSeconds = 0.18;

	private sealed class Entry
	{
		public PresentationModal Modal;
		public float Scale;
		public double Remaining;
		public bool Hovered;
		public Tween Tween;
	}

	/// <summary>Newest first; the newest sits lowest, against the bottom of the strip.</summary>
	private readonly List<Entry> _entries = new();

	/// <summary>Only with pacing on and a strip to sit beside — otherwise the modal just fades as before.</summary>
	public static bool CanAccept =>
		Current != null && IsInstanceValid(Current) && Current.IsInsideTree()
		&& GameHistoryList.Current != null && IsInstanceValid(GameHistoryList.Current)
		&& GameSettings.ShowHideSeconds > 0;

	public override void _Ready()
	{
		SetAnchorsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Ignore;
		// Over the HUD, under the history popup (99) and the modal stack (100).
		ZIndex = 98;
		Current = this;
	}

	public override void _ExitTree()
	{
		if (Current == this) Current = null;
	}

	/// <summary>Take a resolved modal from the row and fly it into the tray.</summary>
	public void Accept(PresentationModal modal)
	{
		modal.Reparent(this);
		modal.SetAnchorsPreset(LayoutPreset.TopLeft, true);
		// Mouse-transparent so the board stays clickable; hover is polled in _Process instead.
		IgnoreMouse(modal);
		// Pivot on the bottom-right corner, so scaling grows up and left, away from the strip and screen edge.
		modal.PivotOffset = modal.Size;

		float scale = Mathf.Min(1f, Mathf.Min(MaxWidth / modal.Size.X, MaxHeight / modal.Size.Y));
		_entries.Insert(0, new Entry
		{
			Modal = modal,
			Scale = scale,
			Remaining = GameSettings.GetFixedDurationSeconds(LingerMilliseconds),
		});

		while (_entries.Count > MaxEntries)
		{
			Entry oldest = _entries[^1];
			_entries.RemoveAt(_entries.Count - 1);
			Leave(oldest);
		}
		Arrange(GameSettings.ShowHideSeconds);
	}

	public override void _Process(double delta)
	{
		if (_entries.Count == 0) return;

		// A modal over the dock owns the mouse; nothing under it should zoom.
		bool blocked = ModalStack.Current?.Visible == true;
		Vector2 mouse = GetLocalMousePosition();
		bool changed = false;

		for (int i = _entries.Count - 1; i >= 0; i--)
		{
			Entry entry = _entries[i];
			if (!IsInstanceValid(entry.Modal))
			{
				_entries.RemoveAt(i);
				changed = true;
				continue;
			}

			bool hovered = !blocked && VisualRect(entry.Modal).HasPoint(mouse);
			if (hovered != entry.Hovered)
			{
				entry.Hovered = hovered;
				if (hovered) entry.Modal.MoveToFront();
				changed = true;
			}

			// The clock stops while the player is reading it.
			if (!hovered) entry.Remaining -= delta;
			if (entry.Remaining <= 0)
			{
				_entries.RemoveAt(i);
				Leave(entry);
				changed = true;
			}
		}

		if (changed) Arrange(HoverSeconds);
	}

	/// <summary>Stack the entries up from the bottom of the history strip, just to its left.</summary>
	private void Arrange(double seconds)
	{
		Vector2 corner = Anchor();
		for (int i = 0; i < _entries.Count; i++)
		{
			Entry entry = _entries[i];
			PresentationModal modal = entry.Modal;
			float scale = entry.Hovered ? Mathf.Min(1f, entry.Scale * HoverZoom) : entry.Scale;
			float alpha = i == 0 || entry.Hovered ? 1f : OlderAlpha;

			entry.Tween?.Kill();
			entry.Tween = modal.CreateTween().SetParallel()
				.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
			// With the pivot on the bottom-right, position + size is the corner that stays put while scaling.
			entry.Tween.TweenProperty(modal, "position", corner - modal.Size, seconds);
			entry.Tween.TweenProperty(modal, "scale", Vector2.One * scale, seconds);
			entry.Tween.TweenProperty(modal, "modulate:a", alpha, seconds);

			// Spaced by the resting size, so a hovered entry zooms over its neighbours rather than shoving them.
			corner.Y -= modal.Size.Y * entry.Scale + EntryGap;
		}
	}

	/// <summary>The bottom-right corner of the lowest entry, in this node's coordinates.</summary>
	private Vector2 Anchor()
	{
		GameHistoryList strip = GameHistoryList.Current;
		if (strip == null || !IsInstanceValid(strip))
			return new Vector2(Size.X - 56f, Size.Y - 250f);
		Rect2 rect = strip.GetGlobalRect();
		return ToDock(new Vector2(rect.Position.X - StripGap, rect.End.Y));
	}

	/// <summary>A discard burns where it lingered; then either one folds into the newest badge.</summary>
	private void Leave(Entry entry)
	{
		entry.Tween?.Kill();
		PresentationModal modal = entry.Modal;
		if (!IsInstanceValid(modal)) return;

		float burnSeconds = (float)GameSettings.DurationLongSeconds;
		if (modal.BurnsInDock && burnSeconds > 0)
		{
			Tween burn = modal.BurnItems(burnSeconds);
			if (burn != null)
			{
				burn.Finished += () => { if (IsInstanceValid(modal)) FoldAway(modal); };
				return;
			}
		}
		FoldAway(modal);
	}

	/// <summary>
	/// Travel into the newest badge, landing at its height, and only then fade. Fading during the trip
	/// hid it: the card set seemed to vanish where it sat rather than go anywhere.
	/// </summary>
	private void FoldAway(PresentationModal modal)
	{
		Control badge = GameHistoryList.Current is { } strip && IsInstanceValid(strip) ? strip.NewestBadge : null;
		Rect2 landing = badge != null && IsInstanceValid(badge)
			? new Rect2(ToDock(badge.GetGlobalRect().Position), badge.GetGlobalRect().Size)
			: new Rect2(modal.Position + modal.Size, Vector2.Zero);

		float scale = Mathf.Max(landing.Size.Y, 8f) / modal.Size.Y;
		// The pivot is the bottom-right corner, so put that corner where a badge-high copy is centred on the badge.
		Vector2 corner = landing.GetCenter() + modal.Size * scale / 2f;

		double seconds = GameSettings.ShowHideSeconds * 1.2;
		Tween tween = modal.CreateTween().SetParallel()
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
		tween.TweenProperty(modal, "position", corner - modal.Size, seconds);
		tween.TweenProperty(modal, "scale", Vector2.One * scale, seconds);
		tween.TweenProperty(modal, "modulate:a", 1f, seconds * 0.3);
		tween.Chain().TweenProperty(modal, "modulate:a", 0f, GameSettings.ShowHideSeconds * 0.4);
		tween.Chain().TweenCallback(Callable.From(modal.QueueFree));
	}

	private Vector2 ToDock(Vector2 global) => GetGlobalTransform().AffineInverse() * global;

	/// <summary>Where the modal is drawn: it scales about its bottom-right corner.</summary>
	private static Rect2 VisualRect(Control modal)
	{
		Vector2 drawn = modal.Size * modal.Scale;
		return new Rect2(modal.Position + modal.Size - drawn, drawn);
	}

	private static void IgnoreMouse(Node node)
	{
		if (node is Control control) control.MouseFilter = MouseFilterEnum.Ignore;
		foreach (Node child in node.GetChildren())
			IgnoreMouse(child);
	}
}
