using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// The side panel that answers "why am I being asked this?" — it shows the thing that caused the
/// current prompt, as a card, with a one-line summary underneath, and beside it a live view of WHERE
/// on the board the thing happened.
///
/// Lives in its own scene rather than inside FactionHandDisplay, where it used to be. That parent is
/// hidden except during a card-selection prompt and its Hide() cleared this panel outright, so it
/// could never appear beside a unit- or country-selection prompt — which is exactly what a scenario
/// mutator raises. Standing on its own, it serves both card reactions and Bulletins.
/// </summary>
public partial class TriggerContextDisplay : Control, LoadableUI
{
	public static TriggerContextDisplay Current;

	private Panel Panel => GetNode<Panel>("%TriggerContextPanel");
	private CardScene CardSceneNode => GetNode<CardScene>("%TriggerCard");
	private RichTextLabel Label => GetNode<RichTextLabel>("%TriggerLabel");
	// Control, not Panel: the node is a PanelContainer so its height follows the text, and only
	// Visible is ever touched here.
	private Control TriggerLabelPanel => GetNode<Control>("%TriggerLabelPanel");
	private Control Column => GetNode<Control>("%TriggerContextColumn");

	// The announced card's flight: a copy drawn under the history strip, handed off to the slot on landing.
	private Control Flight => GetNode<Control>("%AnnouncementFlight");
	private CardScene FlightCard => GetNode<CardScene>("%AnnouncementCard");

	/// <summary>How much bigger than the slot an announced card first pops in.</summary>
	private const float PopInScale = 1.4f;
	private const int SlideMilliseconds = 350;
	private const int ShrinkMilliseconds = 400;
	private const int ToTableMilliseconds = 450;

	/// <summary>Where the popped-in card stops, from the screen's right edge: clear of the 48px history strip.</summary>
	[Export] private float PopInRightMargin = 70f;

	// The announced card outlives a prompt that borrows the slot; _showingAnnouncement is whether it is on screen now.
	private int _announcedCardId = -1;
	private string _announcedTitle;
	private bool _announcedToTable;
	private bool _showingAnnouncement;
	private Tween _flight;
	private TaskCompletionSource _flightDone;


	/// <summary>
	/// The picture-in-picture camera, aimed at the trigger's target while this panel is up. Public
	/// because it is the only handle on the second view of the board — <c>NodeUtilities.FocusCamera</c>
	/// reads it from here, the % name being unique to this scene rather than to Player.tscn.
	/// </summary>
	public Camera2D FocusCamera => GetNodeOrNull<Camera2D>("%FocusCamera");

	/// <summary>
	/// The whole focus frame — shadow, border and the viewport inside them. This is the show/hide
	/// handle, not FocusContainer: the chrome are siblings of the container, so hiding the container
	/// alone would leave a bordered hole on screen.
	/// </summary>
	private Control FocusContainerControl => GetNodeOrNull<Control>("%FocusContainerControl");

	/// <summary>
	/// How far in the focus viewport sits. Wide enough to read the country the camera is pointed at,
	/// which is what the frame is for — the unit inside it is identified by its indicator, not by scale.
	///
	/// The viewport is 350px square, so this is 350/zoom world units across: at 0.4 that is ~875, which
	/// frames all but the largest countries whole. The board itself is 4269 wide — a zoom much below
	/// this stops being a focus view and just shows half the map again.
	/// </summary>
	[Export] private float FocusZoom = 0.4f;

	// The prompt colours, warm for the window that can still stop something and cool for the one that
	// can only answer it. Kept as BBCode hex rather than theme colours because they are only ever
	// interpolated into this one label's text.
	private const string BlockColour    = "#ff9d5c";
	private const string AfterColour    = "#7fb3ff";
	private const string BulletinColour = "#e0c980";
	private const string CauseColour    = "#9a9a9a";

	// One icon per window kind, drawn inline in the label rather than as a node of its own: the label
	// is the only thing in that panel, so a TextureRect beside it would need its own row and its own
	// alignment for no gain. Each SVG is tinted to match the colour above it — see PromptFor.
	private const string BlockIcon    = "res://assets/textures/Other/Icons/BlockWindowIcon.svg";
	private const string AfterIcon    = "res://assets/textures/Other/Icons/AfterReactionIcon.svg";
	private const string BulletinIcon = "res://assets/textures/Other/Icons/BulletinIcon.svg";

	/// <summary>Icon edge in pixels, a little above the text so it reads as the line's marker.</summary>
	private const int IconSize = 22;

	public override void _Ready()
	{
		if (SessionIdentity.IsLocalAuthority(this))
		{
			Current = this;
			LoadUI();
		}
		HideFocus();
	}

	public void LoadUI()
	{
		CardSceneNode.TriggersEmphasis(false);
		CardSceneNode.SetClickable(false);
		Panel.Visible = false;

		FlightCard.TriggersEmphasis(false);
		FlightCard.SetClickable(false);
		FlightCard.SetMousePassthrough(true);
		// Built at pop-in size and scaled down on landing, so its text never renders upscaled.
		Flight.Size = Panel.CustomMinimumSize * PopInScale;
	}

	// AnimationQueue is an autoload: a flight cut short by leaving the game must not leave it awaiting forever.
	public override void _ExitTree()
	{
		_flightDone?.TrySetResult();
		if (Current == this) Current = null;
	}

	/// <summary>Show a real card as the trigger — a played card being reacted to or blocked.</summary>
	/// <param name="kind">
	/// Which window this is, which decides the header and its tense — see <see cref="HeaderFor"/>.
	/// </param>
	/// <param name="causeText">
	/// Straight off <see cref="InputRequest.TriggerCauseText"/>: which card caused the event, already
	/// worded and redacted by <c>GameMessageDisplay.CauseText</c>. Null leaves the line off.
	/// </param>
	/// <param name="targetCountryIds">
	/// Straight off <see cref="InputRequest.TriggerTargetCountryIds"/> — where the triggering event
	/// landed. Null for a trigger that names no place, which leaves the focus viewport hidden.
	/// </param>
	/// <param name="targetUnitIds"><inheritdoc cref="targetCountryIds"/></param>
	public void ShowCard(int cardId, string summaryText, TriggerKind kind = TriggerKind.NONE,
						 string causeText = null,
						 List<int> targetCountryIds = null, List<int> targetUnitIds = null)
	{
		if (Panel == null) return;
		EndAnnouncement();

		if (cardId > -1)
		{
			CardSceneNode.Visible = true;
			CardSceneNode.ShowCard(cardId);
			CardSceneNode.SetActivatable(true);
		}
		else
		{
			CardSceneNode.Visible = false;
		}

		ShowFocus(targetCountryIds, targetUnitIds);
		Show(summaryText, kind, causeText);
	}

	/// <summary>Show just a card, with no summary or focus view: the card whose step is asking, not an event to answer.</summary>
	public void ShowCardOnly(int cardId)
	{
		if (Panel == null) return;
		EndAnnouncement();

		CardSceneNode.Visible = true;
		CardSceneNode.ShowCard(cardId);
		CardSceneNode.SetActivatable(true);
		ShowFocus(null, null);
		Panel.Visible = true;
		TriggerLabelPanel.Visible = false;
	}

	/// <summary>Show a Bulletin as the trigger — a scenario mutator asking this player for input.</summary>
	public void ShowBulletin(CardFace face, string summaryText)
	{
		if (Panel == null) return;
		EndAnnouncement();

		CardSceneNode.Visible = true;
		CardSceneNode.ShowFace(face);
		CardSceneNode.SetActivatable(true);
		// No focus view: a Bulletin is a scenario asking a question, not an event that landed somewhere.
		ShowFocus(null, null);
		Show(summaryText, TriggerKind.BULLETIN);
	}

	private void Show(string summaryText, TriggerKind kind, string causeText = null)
	{
		string cause = string.IsNullOrWhiteSpace(causeText)
			? string.Empty
			: $"\n[color={CauseColour}]Cause: {causeText}[/color]";

		(string prompt, bool summaryOnPromptLine) = PromptFor(kind);
		// A prompt that asks its own question keeps the summary on the next line, as the answer to it.
		// One that is only an icon takes the summary alongside, rather than leaving the icon floating on
		// a line of its own.
		string separator = summaryOnPromptLine ? " " : "\n";

		Label.Text = $"{prompt}{separator}{summaryText ?? string.Empty}{cause}";
		Panel.Visible = true;
		TriggerLabelPanel.Visible = true;
	}

	/// <summary>
	/// The line over the summary: an icon for which window this is, and the question that window is
	/// actually asking.
	///
	/// A question rather than a label, because it is the one framing the summary underneath cannot
	/// contradict. Those summaries are the past-tense lines the history strip shows, and in a BLOCK
	/// window the event has not been applied yet — "Do you want to block this action?" reads correctly
	/// over a sentence describing the action either way, where a bare "BLOCK WINDOW" left the player to
	/// work out whether they were being shown something done or something pending.
	///
	/// The icon carries the kind, in the same colour as the question, so the two windows are
	/// distinguishable before either is read. Swapping the art means changing only the paths above.
	///
	/// The wording lives here rather than on <see cref="TriggerKind"/>: it is UI copy, and the enum
	/// travels over the wire to peers that may render it differently.
	/// </summary>
	/// <returns>
	/// The prompt line, and whether the summary belongs on the same line as it — true when the prompt
	/// asks nothing and is only an icon.
	/// </returns>
	private static (string Prompt, bool SummaryOnPromptLine) PromptFor(TriggerKind kind) => kind switch
	{
		TriggerKind.BLOCK    => (Prompt(BlockIcon, BlockColour, "Do you want to block this action?"), false),
		TriggerKind.AFTER    => (Prompt(AfterIcon, AfterColour, "Do you want to react to this action?"), false),
		// No question of ours: a Bulletin is the scenario asking one of its own, and the summary IS that
		// question — so the icon only says where it came from, and stands beside it.
		TriggerKind.BULLETIN => (Prompt(BulletinIcon, BulletinColour, null), true),
		// A prompt that carries a trigger without being a window of its own — the faction's own play,
		// reached while a reaction is live. It asks nothing about the trigger, so it only names it.
		_                    => ("[b]Reacting to:[/b]", false),
	};

	private static string Prompt(string iconPath, string colour, string question)
	{
		string icon = $"[img={IconSize}x{IconSize}]{iconPath}[/img]";
		return question == null ? icon : $"{icon} [color={colour}][b]{question}[/b][/color]";
	}

	/// <summary>Take a prompt's context down, falling back to the announced card while it is still current.</summary>
	public new void Hide()
	{
		if (_announcedCardId > -1)
		{
			ShowAnnouncement();
			return;
		}

		EndAnnouncement();
		HideFocus();
		if (Panel != null)
			Panel.Visible = false;
	}

	// ── Announced card ───────────────────────────────

	/// <summary>
	/// A played, activated or used card slides out from behind the history strip, holds a moment, then shrinks into the
	/// slot and rests there until a prompt about another card or a step change clears it. Completes once it has landed.
	/// </summary>
	public Task AnnounceCard(int cardId, string title, bool toTable = false)
	{
		if (Panel == null) return Task.CompletedTask;

		SendShownCardToTable();
		_announcedCardId = cardId;
		_announcedTitle = title;
		_announcedToTable = toTable;
		ShowAnnouncement();

		double slide = GameSettings.GetFixedDurationSeconds(SlideMilliseconds);
		if (slide <= 0) return Task.CompletedTask;

		// The slot is laid out now but kept transparent, so its rect has settled by the time the card lands.
		Column.Modulate = Colors.Transparent;
		FlightCard.ShowCard(cardId);
		float screenRight = GetViewportRect().Size.X;
		Flight.Scale = Vector2.One;
		Flight.GlobalPosition = new Vector2(screenRight, Column.GlobalPosition.Y);
		Flight.Visible = true;

		_flightDone = new TaskCompletionSource();
		_flight = CreateTween();
		_flight.TweenProperty(Flight, "global_position:x", screenRight - PopInRightMargin - Flight.Size.X, slide)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		_flight.TweenInterval(GameSettings.DurationLongSeconds);
		_flight.TweenCallback(Callable.From(ShrinkIntoSlot));
		return _flightDone.Task;
	}

	/// <summary>
	/// Drop the announced card unless it is one of <paramref name="cardIds"/>. A prompt's context on screen is
	/// left alone; the announcement just no longer comes back when that prompt closes.
	/// </summary>
	public void ClearAnnouncementUnlessAbout(params int[] cardIds)
	{
		if (!cardIds.Contains(_announcedCardId)) ClearAnnouncement();
	}

	/// <summary>Drop the announced card outright, as a turn step change does.</summary>
	public void ClearAnnouncement()
	{
		if (_announcedCardId < 0) return;
		if (_showingAnnouncement && !SendShownCardToTable()) SlideShownCardOff();
		_announcedCardId = -1;
		if (_showingAnnouncement) Hide();
	}

	/// <summary>
	/// A card announced as going onto the table leaves the slot by shrinking into its faction's Active Cards
	/// button. Fire-and-forget: the slot is free for whatever replaces it at once. False when it did not go.
	/// </summary>
	private bool SendShownCardToTable()
	{
		if (!_showingAnnouncement || !_announcedToTable || _announcedCardId < 0) return false;
		// Still checked against the table: a play that was blocked has gone to the discard pile instead.
		int cardId = _announcedCardId;
		CardState card = CardState.ForId(cardId);
		DeckState deck = card == null ? null : DeckState.ForFaction(card.Faction);
		if (deck == null || !(deck.StatusCardIds.Contains(cardId) || deck.ResponseCardIds.Contains(cardId))) return false;
		if (FactionsContainer.Current?.FactionInfoNodes.GetValueOrDefault(card.Faction) is not { } row
			|| !row.IsVisibleInTree()) return false;
		double seconds = GameSettings.GetFixedDurationSeconds(ToTableMilliseconds);
		if (seconds <= 0) return false;

		Rect2 slot = CardSceneNode.GetGlobalRect();
		CardScene copy = DepartingCopy(cardId, slot);
		Rect2 button = row.ActiveCardsButtonRect;
		float scale = button.Size.Y / slot.Size.Y;
		Tween tween = copy.CreateTween().SetParallel().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
		tween.TweenProperty(copy, "global_position", button.GetCenter() - slot.Size * scale / 2, seconds);
		tween.TweenProperty(copy, "scale", Vector2.One * scale, seconds);
		tween.TweenProperty(copy, "modulate:a", 0f, seconds * 0.4).SetDelay(seconds * 0.6);
		tween.Chain().TweenCallback(Callable.From(copy.QueueFree));
		return true;
	}

	/// <summary>The way back out for a card leaving an empty slot: off to the right, behind the history strip.</summary>
	private void SlideShownCardOff()
	{
		double seconds = GameSettings.GetFixedDurationSeconds(SlideMilliseconds);
		if (seconds <= 0) return;
		int cardId = _announcedCardId;
		Rect2 slot = CardSceneNode.GetGlobalRect();

		// Deferred: the prompt that cleared it may put its own card in the slot straight after, and then it is just replaced.
		Callable.From(() =>
		{
			if (!IsInstanceValid(this) || Panel.Visible) return;
			CardScene copy = DepartingCopy(cardId, slot);
			Tween tween = copy.CreateTween();
			tween.TweenProperty(copy, "global_position:x", GetViewportRect().Size.X, seconds)
				.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
			tween.TweenCallback(Callable.From(copy.QueueFree));
		}).CallDeferred();
	}

	/// <summary>A throwaway copy of the slot's card to animate away, so it can still be going while the next card arrives.</summary>
	private CardScene DepartingCopy(int cardId, Rect2 slot)
	{
		CardScene copy = CardScene.CardScenePackedPath.Instantiate<CardScene>();
		AddChild(copy);
		copy.SetAnchorsPreset(LayoutPreset.TopLeft);
		copy.PivotOffset = Vector2.Zero;
		copy.TriggersEmphasis(false);
		copy.SetClickable(false);
		copy.SetMousePassthrough(true);
		copy.Size = slot.Size;
		copy.GlobalPosition = slot.Position;
		copy.ShowCard(cardId);
		return copy;
	}

	private void ShowAnnouncement()
	{
		ShowCardOnly(_announcedCardId);
		Label.Text = _announcedTitle;
		TriggerLabelPanel.Visible = true;
		_showingAnnouncement = true;
	}

	private void ShrinkIntoSlot()
	{
		Rect2 slot = CardSceneNode.GetGlobalRect();
		double shrink = GameSettings.GetFixedDurationSeconds(ShrinkMilliseconds);
		_flight = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
		_flight.TweenProperty(Flight, "global_position", slot.Position, shrink);
		_flight.TweenProperty(Flight, "scale", Vector2.One * (slot.Size.X / Flight.Size.X), shrink);
		_flight.Chain().TweenCallback(Callable.From(EndFlight));
	}

	private void EndAnnouncement()
	{
		_showingAnnouncement = false;
		EndFlight();
	}

	/// <summary>Land at once: drop the flying copy, reveal the slot and release the animation awaiting it.</summary>
	private void EndFlight()
	{
		_flight?.Kill();
		_flight = null;
		Flight.Visible = false;
		Column.Modulate = Colors.White;
		_flightDone?.TrySetResult();
		_flightDone = null;
	}

	// ── Focus viewport ───────────────────────────────

	/// <summary>
	/// Frame where the trigger landed, and mark the unit it reached.
	///
	/// The COUNTRY frames the shot even when the trigger names a unit. At this zoom the country is what
	/// makes the view readable, and centring on the unit would say nothing the indicator does not say
	/// better — so the camera answers "where" and the mark answers "which". Units are still the
	/// fallback anchor, for a trigger that somehow names one without a country.
	/// </summary>
	private void ShowFocus(List<int> countryIds, List<int> unitIds)
	{
		Control frame = FocusContainerControl;
		Camera2D camera = FocusCamera;
		if (frame == null || camera == null) return;

		// Cleared ahead of the early return as well as after it: a prompt opening with no target must
		// take the previous prompt's marks down with it.
		FocusTargetDisplay.Clear();

		List<Vector2> positions = ResolvePositions(countryIds, CountryPositionOf);
		if (positions.Count == 0) positions = ResolvePositions(unitIds, UnitPositionOf);

		if (positions.Count == 0)
		{
			frame.Visible = false;
			return;
		}

		camera.GlobalPosition = positions.Aggregate(Vector2.Zero, (sum, p) => sum + p) / positions.Count;
		camera.Zoom = new Vector2(FocusZoom, FocusZoom);

		// The whole frame, not FocusContainer: the shadow and the nine-patch border are siblings of the
		// container, so hiding the container alone would leave an empty bordered hole on screen.
		frame.Visible = true;

		// Units only, and never the country: the camera is already pointed at the country, so glowing it
		// too would drown the one mark that says which unit is meant.
		FocusTargetDisplay.Show(unitIds);
	}

	private void HideFocus()
	{
		FocusTargetDisplay.Clear();
		
		if (FocusContainerControl != null)
			FocusContainerControl.Visible = false;
		if(TriggerLabelPanel != null)
			TriggerLabelPanel.Visible = false;
	}

	/// <summary>
	/// Ids to world positions, dropping every one that does not resolve to a place on the board.
	///
	/// These ids arrived over the wire and the state lookups behind them throw on an id they do not
	/// know, so an unresolvable one has to cost this entry and nothing else — the same treatment
	/// <see cref="CardTargetPreviewDisplay"/> gives the ids it filters out of ForIds.
	/// </summary>
	private static List<Vector2> ResolvePositions(List<int> ids, System.Func<int, Vector2?> resolve)
	{
		List<Vector2> positions = new();
		if (ids == null) return positions;

		foreach (int id in ids.Distinct())
		{
			Vector2? position = null;
			try
			{
				position = resolve(id);
			}
			catch (System.Exception e)
			{
				DebugUtilities.PrintPeer($"Focus target {id} did not resolve to a position: {e.Message}");
			}

			if (position.HasValue) positions.Add(position.Value);
		}
		return positions;
	}

	/// <summary>
	/// Only reached as the fallback anchor, when no country resolved. Null for a unit that is no longer
	/// on the board: <c>CountryScene.RemoveUnit</c> parks a removed UnitScene off-board and hides it
	/// there, so its position would aim the camera at nothing.
	/// </summary>
	private static Vector2? UnitPositionOf(int unitId)
	{
		UnitState unitState = UnitState.ForId(unitId);
		if (unitState?.IsDeployedToCountry != true || unitState.UnitScene == null) return null;
		return unitState.UnitScene.GlobalPosition;
	}

	private static Vector2? CountryPositionOf(int countryId)
		=> CountryState.ForId(countryId)?.CountryScene?.GlobalCenter;
}
