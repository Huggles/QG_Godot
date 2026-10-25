using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// The rulebook's card distribution table: how many cards of each type every faction's deck held at the
/// start of the game. Opened from the deck button on the mini menu.
///
/// Counted from the card states the game mode built at setup, expansion additions and removals included.
/// Cards only move between piles after that, so the count never changes — except for Bulletins, which
/// are registered mid-game as Status cards and are left out.
/// </summary>
public partial class DeckCompositionPanel : CenterContainer
{
	/// <summary>The rulebook's row order. A type no deck holds (Air Power and Bolster in a base game) gets no row.</summary>
	private static readonly CardType[] RowOrder =
	{
		CardType.BUILD_ARMY, CardType.BUILD_NAVY, CardType.ECONOMIC_WARFARE, CardType.EVENT, CardType.LAND_BATTLE,
		CardType.RESPONSE, CardType.SEA_BATTLE, CardType.STATUS, CardType.AIR_POWER, CardType.BOLSTER,
	};

	/// <summary>The rulebook's column order.</summary>
	private static readonly Faction[] ColumnOrder =
	{
		Faction.GERMANY, Faction.ITALY, Faction.JAPAN, Faction.UNITED_KINGDOM, Faction.SOVIET, Faction.UNITED_STATES,
	};

	private const int FontSize = 14;
	private const float FactionColumnWidth = 84f;

	/// <summary>Must match the Plate stylebox. Cells are blended onto it rather than drawn translucent, so the map never shows through.</summary>
	private static readonly Color PlateColor = new(0.1f, 0.1f, 0.1f);
	private static readonly Color RowLabelColor = PlateColor.Lerp(Colors.White, 0.07f);
	private static readonly Color NumberColor = new(0.94f, 0.94f, 0.94f);
	private static readonly Color ZeroColor = new(1f, 1f, 1f, 0.35f);

	private GridContainer Table => GetNode<GridContainer>("%Table");
	private Button CloseButton => GetNode<Button>("%CloseButton");

	private bool _swallowEscapeRelease;

	public override void _Ready()
	{
		Visible = false;
		CloseButton.Pressed += Close;
	}

	public void Toggle()
	{
		if (Visible)
		{
			Close();
			return;
		}
		Rebuild();
		Visible = true;
	}

	private void Close() => Visible = false;

	/// <summary>
	/// Escape closes the panel. Swallows the release as well: ModalStack acts on the release, and would
	/// otherwise park the prompt this panel was drawn over in the same key press.
	/// </summary>
	public override void _UnhandledKeyInput(InputEvent @event)
	{
		if (@event is not InputEventKey { Keycode: Key.Escape } key) return;

		if (!key.Pressed && _swallowEscapeRelease)
		{
			_swallowEscapeRelease = false;
			GetViewport().SetInputAsHandled();
			return;
		}
		if (!Visible || !key.Pressed || key.Echo) return;

		Close();
		_swallowEscapeRelease = true;
		GetViewport().SetInputAsHandled();
	}

	private void Rebuild()
	{
		// RemoveChild before QueueFree: a queued child still counts in GetChildCount this frame.
		foreach (Node child in Table.GetChildren())
		{
			Table.RemoveChild(child);
			child.QueueFree();
		}

		Dictionary<(Faction, CardType), int> counts = CountAtStart();
		// A faction outside the rulebook's columns (a minor faction with a deck of its own) goes last.
		List<Faction> factions = counts.Keys.Select(key => key.Item1).Distinct()
			.OrderBy(faction => System.Array.IndexOf(ColumnOrder, faction) is var index and >= 0 ? index : ColumnOrder.Length)
			.ToList();
		List<CardType> types = RowOrder.Where(type => factions.Any(faction => counts.ContainsKey((faction, type)))).ToList();

		Table.Columns = factions.Count + 1;
		Table.AddChild(new Control { MouseFilter = MouseFilterEnum.Ignore });
		foreach (Faction faction in factions)
			Table.AddChild(HeaderCell(faction));

		for (int row = 0; row < types.Count; row++)
		{
			Table.AddChild(Cell(types[row].Label(), RowLabelColor, NumberColor, HorizontalAlignment.Left));
			// Alternate rows a shade apart so a row can be read across six tinted columns.
			float tint = row % 2 == 0 ? 0.30f : 0.22f;
			foreach (Faction faction in factions)
				Table.AddChild(NumberCell(counts.GetValueOrDefault((faction, types[row])), faction, tint));
		}

		Table.AddChild(Cell("Total", RowLabelColor, Colors.White, HorizontalAlignment.Left));
		foreach (Faction faction in factions)
			Table.AddChild(NumberCell(types.Sum(type => counts.GetValueOrDefault((faction, type))), faction, 0.5f));
	}

	private static Dictionary<(Faction, CardType), int> CountAtStart()
	{
		Dictionary<(Faction, CardType), int> counts = new();
		foreach (CardState card in GameSession.Current.GameState.CardStates)
		{
			if (card is BulletinCardState || !RowOrder.Contains(card.CardData.CardType)) continue;

			(Faction, CardType) key = (card.Faction, card.CardData.CardType);
			counts[key] = counts.GetValueOrDefault(key) + 1;
		}
		return counts;
	}

	private static Control HeaderCell(Faction faction)
	{
		FactionData data = StaticGameData.FactionDataMap.GetValueOrDefault(faction);
		Control cell = Cell(faction.Label(), data?.FactionColor ?? Colors.Gray, data?.FactionColorText ?? Colors.White, HorizontalAlignment.Center);
		// Wraps "United Kingdom" over two lines at the column's width, as the rulebook does.
		Label label = cell.GetChild<Label>(0);
		label.CustomMinimumSize = new Vector2(FactionColumnWidth, 0);
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		return cell;
	}

	private static Control NumberCell(int count, Faction faction, float tint)
	{
		Color background = PlateColor.Lerp(StaticGameData.FactionDataMap.GetValueOrDefault(faction)?.FactionColor ?? Colors.Gray, tint);
		return count == 0
			? Cell("—", background, ZeroColor, HorizontalAlignment.Center)
			: Cell(count.ToString(), background, NumberColor, HorizontalAlignment.Center);
	}

	private static Control Cell(string text, Color background, Color fontColor, HorizontalAlignment alignment)
	{
		PanelContainer cell = new() { MouseFilter = MouseFilterEnum.Ignore };
		cell.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = background,
			ContentMarginLeft = 10,
			ContentMarginRight = 10,
			ContentMarginTop = 5,
			ContentMarginBottom = 5,
		});

		Label label = new()
		{
			Text = text,
			HorizontalAlignment = alignment,
			VerticalAlignment = VerticalAlignment.Center,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		label.AddThemeFontSizeOverride("font_size", FontSize);
		label.AddThemeColorOverride("font_color", fontColor);
		cell.AddChild(label);
		return cell;
	}
}
