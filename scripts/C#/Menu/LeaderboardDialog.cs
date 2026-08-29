using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// The ranked ladder, read straight off the Steam leaderboard named <see cref="LeaderboardName"/>.
///
/// Read-only: nothing here uploads a score. Every entry Steam returns is listed, best rank first.
///
/// An unranked player is normally seeded onto the board at <see cref="StartingElo"/> before this window
/// ever opens — see <c>MainMenu.EnableLeaderboardIfAvailableAsync</c>. The local row this class invents
/// when they are still missing covers the cases where that did not happen: the seeding write failed, or
/// Steam has not caught up yet.
///
/// Layout lives in <c>res://scenes/menu/LeaderboardDialog.tscn</c>, an inherited scene of
/// <see cref="MenuModal"/>'s shell — see that class for how the pair fit together.
/// </summary>
public partial class LeaderboardDialog : MenuModal
{
	/// <summary>Must match the leaderboard's name in the Steamworks partner site, exactly.</summary>
	public const string LeaderboardName = "Ranked_Base_Game";

	/// <summary>The Elo every player starts on, and what an unranked player is seeded onto the board at.</summary>
	public const int StartingElo = 1500;

	private static readonly Color ColorInfo  = new(0.73f, 0.73f, 0.73f, 1.0f);
	private static readonly Color ColorError = new(0.85f, 0.54f, 0.54f, 1.0f);

	/// <summary>Background for the row belonging to whoever is sitting at this machine.</summary>
	private static readonly Color RowColor      = new(0.15f, 0.15f, 0.15f, 0.55f);
	private static readonly Color RowColorLocal = new(0.20f, 0.28f, 0.20f, 0.70f);

	private static readonly PackedScene Scene =
		GD.Load<PackedScene>("res://scenes/menu/LeaderboardDialog.tscn");

	private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);

	private VBoxContainer _entryList;
	private Label         _statusLabel;
	private Button        _refreshButton;
	private Button        _closeButton;

	/// <summary>steamId → that row's name label, so a late-arriving persona name can fill itself in.</summary>
	private readonly Dictionary<ulong, Label> _nameSlots = new();

	/// <summary>Stops a second Refresh press stacking another download on top of the first.</summary>
	private bool _loading;

	/// <summary>
	/// Opens the window over <paramref name="parent"/> and completes when it closes. Always completes —
	/// _ExitTree resolves it — so a scene change cannot leave the caller awaiting forever.
	/// </summary>
	public static Task ShowAsync(Node parent)
	{
		LeaderboardDialog dialog = Scene.Instantiate<LeaderboardDialog>();
		parent.AddChild(dialog);
		return dialog._closed.Task;
	}

	// Scene wiring: a GetNode failure here means a broken .tscn, which is a real bug worth
	// surfacing rather than a silent console line.
	public override void _Ready()
	{
		base._Ready();
		Guard.Try(ReadyInternal, "LeaderboardDialog._Ready");
	}

	private void ReadyInternal()
	{
		_entryList     = GetNode<VBoxContainer>("%EntryList");
		_statusLabel   = GetNode<Label>("%StatusLabel");
		_refreshButton = GetNode<Button>("%RefreshButton");
		_closeButton   = GetNode<Button>("%CloseButton");

		_refreshButton.Pressed += OnRefreshPressed;
		_closeButton.Pressed   += Cancel;

		if (SteamworksApi.Instance != null)
			SteamworksApi.Instance.UserInfoUpdated += OnUserInfoUpdated;

		OnRefreshPressed();
	}

	public override void _ExitTree()
	{
		if (SteamworksApi.Instance != null)
			SteamworksApi.Instance.UserInfoUpdated -= OnUserInfoUpdated;

		_closed.TrySetResult();
	}

	protected override void Cancel()
	{
		if (Resolved) return;
		Resolved = true;
		QueueFree();
	}

	// ══════════════════════════════════════════════════════════════════════════
	// Load
	// ══════════════════════════════════════════════════════════════════════════

	private void OnRefreshPressed()
	{
		if (Resolved || _loading) return;
		_loading = true;
		Guard.FireAndForget(LoadAsync, "LeaderboardDialog.Load");
	}

	private async Task LoadAsync()
	{
		try
		{
			ClearRows();

			if (!SteamworksApi.IsAvailable)
			{
				SetStatus($"Steam is unavailable: {SteamworksApi.UnavailableReason}.", ColorError);
				return;
			}

			_refreshButton.Disabled = true;
			SetStatus("Loading rankings…", ColorInfo);

			List<LeaderboardRow> rows = await SteamworksApi.Instance.GetLeaderboardAsync(LeaderboardName);

			// The download takes a round trip to Steam, and the player can close the window during it.
			if (!IsInstanceValid(this) || Resolved) return;

			Rebuild(rows);
		}
		finally
		{
			if (IsInstanceValid(this))
			{
				_loading = false;
				if (!Resolved) _refreshButton.Disabled = false;
			}
		}
	}

	// ══════════════════════════════════════════════════════════════════════════
	// List
	// ══════════════════════════════════════════════════════════════════════════

	private void Rebuild(List<LeaderboardRow> rows)
	{
		ClearRows();

		ulong localId    = SteamworksApi.Instance.LocalSteamId;
		bool  localRanked = false;

		_entryList.AddChild(BuildHeader());

		foreach (LeaderboardRow row in rows)
		{
			bool isLocal = row.SteamId == localId;
			localRanked |= isLocal;
			_entryList.AddChild(BuildRow($"{row.Rank}", row.SteamId, row.Score, isLocal));
		}

		if (!localRanked)
		{
			// Seeding should already have given them an entry, so getting here means it did not land.
			// Shown with no rank, because they genuinely do not have one yet.
			_entryList.AddChild(BuildRow("—", localId, StartingElo, isLocal: true));
		}

		if (rows.Count == 0)
		{
			SetStatus("Nobody has been ranked yet — you are the first.", ColorInfo);
			return;
		}

		SetStatus(
			localRanked
				? $"{rows.Count} ranked player(s)."
				: $"{rows.Count} ranked player(s). Your entry has not appeared yet — you start at {StartingElo}.",
			ColorInfo);
	}

	private void ClearRows()
	{
		foreach (Node child in _entryList.GetChildren())
			child.QueueFree();

		// The rows are only queued for deletion, so the slots must be dropped explicitly — otherwise a
		// name arriving a frame later would write into a Label that is on its way out.
		_nameSlots.Clear();
	}

	private static PanelContainer BuildHeader()
	{
		PanelContainer panel = MakePanel(new Color(0, 0, 0, 0));
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 10);
		panel.AddChild(row);

		row.AddChild(MakeLabel("#",      18, new Vector2(60, 0),  ColorInfo));
		row.AddChild(MakeLabel("Player", 18, Vector2.Zero,        ColorInfo, expand: true));
		row.AddChild(MakeLabel("Rating", 18, new Vector2(90, 0),  ColorInfo, alignRight: true));

		return panel;
	}

	/// <summary>
	/// One leaderboard row, styled like <see cref="InviteFriendsDialog"/>'s friend rows.
	///
	/// The name may not be known yet: on a global board hardly anybody is a friend, so Steam has to fetch
	/// most of these. The label is registered in <see cref="_nameSlots"/> and filled in by
	/// <see cref="OnUserInfoUpdated"/> when the answer lands, so the list does not reflow.
	/// </summary>
	private PanelContainer BuildRow(string rank, ulong steamId, int score, bool isLocal)
	{
		PanelContainer panel = MakePanel(isLocal ? RowColorLocal : RowColor);

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 10);
		panel.AddChild(row);

		row.AddChild(MakeLabel(rank, 20, new Vector2(60, 0), ColorInfo));

		Label name = MakeLabel(NameFor(steamId, isLocal), 20, Vector2.Zero, Colors.White, expand: true);
		name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		if (!isLocal) _nameSlots[steamId] = name;
		row.AddChild(name);

		row.AddChild(MakeLabel($"{score}", 20, new Vector2(90, 0), Colors.White, alignRight: true));

		return panel;
	}

	/// <summary>
	/// An ellipsis rather than the raw 64-bit id while a name is still in flight: the rows are ranked, so
	/// nothing is lost by leaving one briefly unnamed, and an id tells the player nothing.
	/// </summary>
	private static string NameFor(ulong steamId, bool isLocal)
	{
		if (isLocal) return SteamworksApi.Instance.LocalPersonaName;
		return SteamworksApi.Instance.PersonaNameOrRequest(steamId) ?? "…";
	}

	private void OnUserInfoUpdated(ulong steamId)
	{
		if (Resolved || !IsInstanceValid(this)) return;
		if (!_nameSlots.TryGetValue(steamId, out Label slot) || !IsInstanceValid(slot)) return;

		string name = SteamworksApi.Instance.PersonaNameOrRequest(steamId);
		if (name != null) slot.Text = name;
	}

	// ══════════════════════════════════════════════════════════════════════════
	// Widgets
	// ══════════════════════════════════════════════════════════════════════════

	private static PanelContainer MakePanel(Color background)
	{
		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor                = background,
			CornerRadiusTopLeft    = 4, CornerRadiusTopRight    = 4,
			CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
			ContentMarginLeft = 10, ContentMarginRight  = 10,
			ContentMarginTop  = 8,  ContentMarginBottom = 8,
		});
		return panel;
	}

	private static Label MakeLabel(string text, int fontSize, Vector2 minSize, Color color,
		bool expand = false, bool alignRight = false)
	{
		var label = new Label
		{
			Text              = text,
			CustomMinimumSize = minSize,
			VerticalAlignment = VerticalAlignment.Center,
		};
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", color);

		// Qualified: this class is a CanvasLayer, not a Control, so the nested enum is not in scope.
		if (expand)     label.SizeFlagsHorizontal   = Control.SizeFlags.ExpandFill;
		if (alignRight) label.HorizontalAlignment   = HorizontalAlignment.Right;

		return label;
	}

	private void SetStatus(string message, Color color)
	{
		_statusLabel.Text = message;
		_statusLabel.AddThemeColorOverride("font_color", color);
	}
}
