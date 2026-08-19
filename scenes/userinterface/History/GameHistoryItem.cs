using System.Collections.Generic;
using Godot;

/// <summary>
/// One entry in the game history strip, drawn as a compact faction badge: the faction's flag, the
/// entry's sequence number, and an icon for what happened. The detail — the summary text and the card
/// that triggered it — lives in <see cref="HistoryDetailPopup"/>, which this item opens on hover.
///
/// Badges rather than text rows so the strip can stay on screen permanently. The old rows were 200px
/// wide and had to fade themselves out after five seconds to give the board back, which meant the
/// history could not be read on demand at all.
/// </summary>
public partial class GameHistoryItem : Control
{
	private TextureRect Flag => GetNode<TextureRect>("%Flag");
	private Panel Tint => GetNode<Panel>("%Tint");
	private TextureRect Icon => GetNode<TextureRect>("%Icon");
	private Label Number => GetNode<Label>("%Number");

	/// <summary>Faction colour wash over the flag, low enough that the flag still reads through it.</summary>
	private const float TintAlpha = 0.35f;

	/// <summary>
	/// Fixed, not a GameSettings duration: DurationMedium is 3s on Slow speed, and a badge that takes
	/// three seconds to appear is not feedback.
	/// </summary>
	private const double AppearSeconds = 0.15;

	private GameHistoryEntry entry;
	private Tween appearTween;

	public static GameHistoryItem Create(GameHistoryEntry entry)
	{
		GameHistoryItem item = AssetRepository.GameHistoryItemScenePackaged.Instantiate<GameHistoryItem>();
		item.entry = entry;
		item.Paint();
		return item;
	}

	private void Paint()
	{
		// StaticGameData rather than FactionState.ForEnum: ForEnum dereferences MultiplayerSession and
		// returns null for a faction that is not in the current state. Same reasoning as
		// FactionDisplay.Label.
		FactionData data = StaticGameData.FactionDataMap.GetValueOrDefault(entry.Faction);

		Number.Text = entry.Sequence.ToString();
		// Duplicated per item: a LabelSettings sub-resource is shared across every instance of the
		// PackedScene, so recolouring the shared one would recolour every badge in the strip. Same
		// reason FactionInfoRow duplicates ClearLabelSettings.tres before touching it.
		Number.LabelSettings = (LabelSettings)Number.LabelSettings.Duplicate();
		Number.LabelSettings.FontColor = data?.FactionColorText ?? Colors.White;

		// GD.Load rather than a preloaded table: ResourceLoader caches, so the repeat cost is a
		// dictionary hit, and keeping the load here means a headless run — which never builds a badge
		// — never touches a texture. Null for a message type that has no icon yet, which leaves the
		// badge reading as flag plus number.
		Icon.Texture = string.IsNullOrEmpty(entry.IconPath) ? null : GD.Load<Texture2D>(entry.IconPath);

		// Faction.NONE and Faction.ALL have no row in FactionData.FactionFlags, so FlagTexture (a raw
		// indexer) would throw KeyNotFoundException. Not a corner case: ChangeStepChangeEvent and
		// ChangeRoundChangeEvent are both NONE, so they hit this on every single turn step.
		bool hasFlag = data != null && FactionData.FactionFlags.ContainsKey(entry.Faction);
		Flag.Visible = hasFlag;
		Flag.Texture = hasFlag ? data.FlagTexture : null;
		Tint.SelfModulate = hasFlag
			? new Color(data.FactionColor, TintAlpha)
			: new Color(Colors.Gray, 0.85f);
	}

	public override void _Ready()
	{
		base._Ready();

		MouseEntered += OnMouseEntered;
		MouseExited += OnMouseExited;

		Modulate = new Color(1, 1, 1, 0);
		appearTween = CreateTween();
		appearTween.TweenProperty(this, "modulate:a", 1.0, AppearSeconds);
	}

	public override void _ExitTree()
	{
		base._ExitTree();
		appearTween?.Kill();
		// Trimmed out of the window, or the whole UI torn down, while this badge owned the popup.
		// IsInstanceValid because a static handle to a freed Godot object throws on access rather
		// than reading as null.
		if (GodotObject.IsInstanceValid(HistoryDetailPopup.Current))
		{
			HistoryDetailPopup.Current.HideFor(this);
		}
	}

	private void OnMouseEntered()
	{
		if (GodotObject.IsInstanceValid(HistoryDetailPopup.Current))
		{
			HistoryDetailPopup.Current.ShowFor(this, entry);
		}
	}

	private void OnMouseExited()
	{
		if (GodotObject.IsInstanceValid(HistoryDetailPopup.Current))
		{
			HistoryDetailPopup.Current.HideFor(this);
		}
	}
}
