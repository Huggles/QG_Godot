using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// The hover detail for a pile pill on a faction flag: the discard, status or response cards, drawn
/// as real cards beside the pill. One instance in the Interface layer, the same shape as
/// <see cref="HistoryDetailPopup"/>: it never takes the mouse, so the pill's own enter/exit drive it.
/// </summary>
public partial class PileDetailPopup : Control, LoadableUI
{
	public static PileDetailPopup Current;

	private const float GapFromFlag = 6f;
	private const float ScreenMargin = 10f;
	/// <summary>Must match PileGrid's h/v_separation in the scene.</summary>
	private const float CardGap = 6f;
	/// <summary>The Margin node's margins plus the panel stylebox's.</summary>
	private const float MarginChrome = 16f;
	/// <summary>Height kept free for the title, the footer and the gaps between them.</summary>
	private const float TextReserve = 80f;
	private const float TextWidthAlone = 170f;
	/// <summary>Between the hand (150x210) and the hover preview (250x350); a big pile shrinks them to fit.</summary>
	private static readonly Vector2 FullCardSize = new(200, 280);

	private PanelContainer DetailPanel => GetNode<PanelContainer>("%PileDetailPanel");
	private Label TitleLabel => GetNode<Label>("%PileTitle");
	private GridContainer Grid => GetNode<GridContainer>("%PileGrid");
	private Label EmptyLabel => GetNode<Label>("%PileEmpty");
	private RichTextLabel FooterLabel => GetNode<RichTextLabel>("%PileFooter");

	/// <summary>Card slots, grown on demand and hidden rather than freed between hovers.</summary>
	private readonly List<CardScene> cards = new();

	/// <summary>The pill the popup belongs to; <see cref="HideFor"/> from any other pill is a no-op.</summary>
	private Control owner;
	/// <summary>The flag the pill sits on: the popup opens just past its right side.</summary>
	private Control flag;

	public override void _Ready()
	{
		if (SessionIdentity.IsLocalAuthority(this))
		{
			Current = this;
			LoadUI();
		}
	}

	public void LoadUI()
	{
		DetailPanel.Visible = false;
		SetProcess(false);
	}

	public override void _ExitTree()
	{
		base._ExitTree();
		if (Current == this) Current = null;
	}

	/// <summary>Open the popup for <paramref name="pill"/>, against the right side of <paramref name="flag"/>.</summary>
	public void ShowFor(Control pill, Control flag, string title, IEnumerable<int> cardIds, string footer = null)
	{
		owner = pill;
		this.flag = flag;
		// CardFace.ForCard NREs on an id that is not in game state. ShowCard draws a face-down
		// Response as its card back, so an opponent's responses stay hidden.
		List<int> ids = cardIds.Where(id => CardState.ForId(id) != null).ToList();

		TitleLabel.Text = $"{title} ({ids.Count})";
		EmptyLabel.Visible = ids.Count == 0;
		Grid.Visible = ids.Count > 0;

		Vector2 cardSize = LayoutGrid(ids.Count, flag);
		for (int i = 0; i < ids.Count; i++)
		{
			CardScene slot = CardAt(i);
			slot.CustomMinimumSize = cardSize;
			slot.Size = cardSize;
			slot.ShowCard(ids[i]);
		}
		for (int i = ids.Count; i < cards.Count; i++) cards[i].Visible = false;

		float gridWidth = ids.Count == 0 ? 0f : Grid.Columns * cardSize.X + CardGap * (Grid.Columns - 1);
		FooterLabel.Visible = !string.IsNullOrEmpty(footer);
		FooterLabel.Text = footer ?? "";
		FooterLabel.CustomMinimumSize = new Vector2(Mathf.Max(TextWidthAlone, gridWidth), 0);

		DetailPanel.Visible = true;
		Reposition(flag);
		SetProcess(true);
	}

	/// <summary>Picks the column count that draws <paramref name="count"/> cards largest in the room right of the flag.</summary>
	private Vector2 LayoutGrid(int count, Control flag)
	{
		Grid.Columns = Mathf.Max(count, 1);
		if (count <= 1) return FullCardSize;

		Rect2 bounds = GetGlobalRect();
		float chrome = MarginChrome + (DetailPanel.GetThemeStylebox("panel")?.GetMinimumSize().X ?? 0f);
		float width = bounds.End.X - ScreenMargin - (flag.GetGlobalRect().End.X + GapFromFlag) - chrome;
		float height = bounds.Size.Y - 2 * ScreenMargin - chrome - TextReserve;

		float bestScale = 0f;
		for (int cols = count; cols >= 1; cols--)
		{
			int rows = Mathf.CeilToInt(count / (float)cols);
			float scale = Mathf.Min(1f, Mathf.Min(
				(width - CardGap * (cols - 1)) / (cols * FullCardSize.X),
				(height - CardGap * (rows - 1)) / (rows * FullCardSize.Y)));
			if (scale > bestScale)
			{
				bestScale = scale;
				Grid.Columns = cols;
			}
		}
		return (FullCardSize * Mathf.Max(bestScale, 0.1f)).Floor();
	}

	private CardScene CardAt(int index)
	{
		if (index < cards.Count) return cards[index];

		CardScene card = CardScene.CardScenePackedPath.Instantiate<CardScene>();
		Grid.AddChild(card);
		card.TriggersEmphasis(false);   // must not drive the hand's card preview
		card.SetClickable(false);
		card.SetActivatable(true);      // clears the red "cannot use this" scrim
		card.SetMousePassthrough(true); // the popup must not take the mouse off the pill
		cards.Add(card);
		return card;
	}

	/// <summary>Release the popup, but only if <paramref name="pill"/> still owns it.</summary>
	public void HideFor(Control pill)
	{
		if (owner == pill) Release();
	}

	/// <summary>Catches the hovers that end without a MouseExited, such as a modal opening over a still cursor.</summary>
	public override void _Process(double delta)
	{
		if (owner == null || !IsInstanceValid(owner) || !owner.IsVisibleInTree()
			|| !owner.GetGlobalRect().HasPoint(owner.GetGlobalMousePosition()) || !IsInstanceValid(flag))
		{
			Release();
			return;
		}
		Reposition(flag);
	}

	private void Release()
	{
		owner = null;
		SetProcess(false);
		if (DetailPanel != null) DetailPanel.Visible = false;
	}

	/// <summary>Just past the flag's right side, centred on the flag and clamped on screen.</summary>
	private void Reposition(Control flag)
	{
		Vector2 size = DetailPanel.GetCombinedMinimumSize();
		DetailPanel.Size = size;

		Rect2 anchor = flag.GetGlobalRect();
		Rect2 bounds = GetGlobalRect();
		float x = Mathf.Min(anchor.End.X + GapFromFlag, bounds.End.X - size.X - ScreenMargin);
		float y = Mathf.Clamp(
			anchor.Position.Y + anchor.Size.Y * 0.5f - size.Y * 0.5f,
			bounds.Position.Y + ScreenMargin,
			Mathf.Max(bounds.Position.Y + ScreenMargin, bounds.End.Y - size.Y - ScreenMargin));
		DetailPanel.GlobalPosition = new Vector2(x, y);
	}
}
