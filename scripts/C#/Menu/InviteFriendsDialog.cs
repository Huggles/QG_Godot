using Godot;
using System.Collections.Generic;

/// <summary>
/// Lists the host's online Steam friends and invites one straight to the current lobby.
///
/// Replaces the Steam overlay invite dialog: that overlay does not render in the editor at all, which made
/// the whole invite path untestable during development, and it threw the player out of the game's own UI
/// for what should be one click.
///
/// Deliberately shows every friend who is not offline rather than only those who own the game — the Steam
/// client API has no ownership query. Friends currently running the game are sorted to the top and tagged;
/// see <see cref="SteamworksApi.GetOnlineFriends"/>.
///
/// Layout lives in <c>res://scenes/menu/InviteFriendsDialog.tscn</c>, an inherited scene of
/// <see cref="MenuModal"/>'s shell — see that class for how the pair fit together.
/// </summary>
public partial class InviteFriendsDialog : MenuModal
{
	/// <summary>How long an invite button stays spent before it can be pressed again.</summary>
	private const double InviteCooldownSeconds = 15.0;

	private static readonly Color ColorInfo  = new(0.73f, 0.73f, 0.73f, 1.0f);
	private static readonly Color ColorError = new(0.85f, 0.54f, 0.54f, 1.0f);

	private static readonly PackedScene Scene =
		GD.Load<PackedScene>("res://scenes/menu/InviteFriendsDialog.tscn");

	private VBoxContainer _friendList;
	private Label         _statusLabel;
	private Button        _refreshButton;
	private Button        _closeButton;

	/// <summary>steamId → that row's avatar slot, so a late-arriving avatar can fill in without a rebuild.</summary>
	private readonly Dictionary<ulong, TextureRect> _avatarSlots = new();

	/// <summary>
	/// Invites already sent. Held on the dialog rather than on the button so a rebuild — a refresh, or
	/// someone joining — does not make a sent invite look unsent.
	/// </summary>
	private readonly HashSet<ulong> _invited = new();

	/// <summary>
	/// Opens the dialog over <paramref name="parent"/>. Nothing to await: unlike the host/join dialogs this
	/// one produces no result, it just dismisses itself.
	/// </summary>
	public static void Show(Node parent) => parent.AddChild(Scene.Instantiate<InviteFriendsDialog>());

	// Scene wiring: a GetNode failure here means a broken .tscn, which is a real bug worth
	// surfacing rather than a silent console line.
	public override void _Ready()
	{
		base._Ready();
		Guard.Try(ReadyInternal, "InviteFriendsDialog._Ready");
	}

	private void ReadyInternal()
	{
		_friendList    = GetNode<VBoxContainer>("%FriendList");
		_statusLabel   = GetNode<Label>("%StatusLabel");
		_refreshButton = GetNode<Button>("%RefreshButton");
		_closeButton   = GetNode<Button>("%CloseButton");

		_refreshButton.Pressed += Rebuild;
		_closeButton.Pressed   += Cancel;

		if (SteamworksApi.Instance != null)
		{
			SteamworksApi.Instance.AvatarUpdated          += OnAvatarUpdated;
			// So a row flips to "In lobby" the moment an invitee actually arrives, without a refresh.
			SteamworksApi.Instance.LobbyMembershipChanged += Rebuild;
		}

		Rebuild();
	}

	public override void _ExitTree()
	{
		if (SteamworksApi.Instance == null) return;
		SteamworksApi.Instance.AvatarUpdated          -= OnAvatarUpdated;
		SteamworksApi.Instance.LobbyMembershipChanged -= Rebuild;
	}

	protected override void Cancel()
	{
		if (Resolved) return;
		Resolved = true;
		QueueFree();
	}

	// ══════════════════════════════════════════════════════════════════════════
	// List
	// ══════════════════════════════════════════════════════════════════════════

	private void Rebuild()
	{
		if (Resolved || !IsInstanceValid(this)) return;

		ClearRows();

		if (!SteamworksApi.IsAvailable)
		{
			SetStatus($"Steam is unavailable: {SteamworksApi.UnavailableReason}.", ColorError);
			return;
		}

		if (SteamworksApi.Instance.CurrentLobbyId == 0)
		{
			SetStatus("You are not in a Steam lobby, so there is nothing to invite anyone to.", ColorError);
			return;
		}

		List<SteamFriend> friends = SteamworksApi.Instance.GetOnlineFriends();
		foreach (SteamFriend friend in friends)
			_friendList.AddChild(BuildRow(friend));

		SetStatus(
			friends.Count == 0
				? "None of your friends are online right now."
				: $"{friends.Count} friend(s) online. Friends already playing are listed first.",
			ColorInfo);
	}

	private void ClearRows()
	{
		foreach (Node child in _friendList.GetChildren())
			child.QueueFree();

		// The rows are only queued for deletion, so the slots must be dropped explicitly — otherwise an
		// avatar arriving a frame later would write into a TextureRect that is on its way out.
		_avatarSlots.Clear();
	}

	/// <summary>One friend row, styled like <see cref="SteamFriendLobbiesScreen"/>'s lobby rows.</summary>
	private PanelContainer BuildRow(SteamFriend friend)
	{
		var panel = new PanelContainer();
		var style = new StyleBoxFlat
		{
			BgColor            = new Color(0.15f, 0.15f, 0.15f, 0.55f),
			CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
			CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
			ContentMarginLeft = 10, ContentMarginRight = 10,
			ContentMarginTop  = 8,  ContentMarginBottom = 8,
		};
		panel.AddThemeStyleboxOverride("panel", style);

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 10);
		panel.AddChild(row);

		// Texture may be null: Steam downloads avatars lazily and answers on AvatarUpdated. The slot is
		// added either way so the row does not reflow once the image lands.
		var avatar = new TextureRect
		{
			CustomMinimumSize = new Vector2(32, 32),
			ExpandMode        = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode       = TextureRect.StretchModeEnum.KeepAspectCentered,
			Texture           = SteamworksApi.Instance.SmallAvatarFor(friend.SteamId),
		};
		_avatarSlots[friend.SteamId] = avatar;
		row.AddChild(avatar);

		row.AddChild(MakeLabel(friend.Name, 22, new Vector2(260, 0), Colors.White));

		Label status = MakeLabel(StatusText(friend), 20, Vector2.Zero, ColorInfo);
		// Qualified: unlike SteamFriendLobbiesScreen this class is a CanvasLayer, not a Control, so the
		// nested enum is not in scope unqualified.
		status.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		status.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		row.AddChild(status);

		// A plain Button, not MenuPanelButton: this is a compact list row rather than menu chrome, and
		// the default styling suits it. Disabled genuinely blocks the click in both widgets.
		var invite = new Button { CustomMinimumSize = new Vector2(120, 44) };
		if (friend.AlreadyInLobby)
		{
			invite.Text     = "In lobby";
			invite.Disabled = true;
		}
		else if (_invited.Contains(friend.SteamId))
		{
			invite.Text     = "Invited";
			invite.Disabled = true;
		}
		else
		{
			invite.Text = "Invite";
			SteamFriend capFriend = friend;
			Button      capButton = invite;
			invite.Pressed += () => OnInvitePressed(capFriend, capButton);
		}
		row.AddChild(invite);

		return panel;
	}

	private static string StatusText(SteamFriend friend)
	{
		if (friend.InThisGame) return "In game";
		return friend.Presence switch
		{
			SteamPresence.LookingToPlay => "Looking to play",
			SteamPresence.Away          => "Away",
			SteamPresence.Busy          => "Busy",
			SteamPresence.Snooze        => "Snoozing",
			_                           => "Online",
		};
	}

	private static Label MakeLabel(string text, int fontSize, Vector2 minSize, Color color)
	{
		var label = new Label { Text = text, CustomMinimumSize = minSize, VerticalAlignment = VerticalAlignment.Center };
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", color);
		return label;
	}

	// ══════════════════════════════════════════════════════════════════════════
	// Invite
	// ══════════════════════════════════════════════════════════════════════════

	private void OnInvitePressed(SteamFriend friend, Button button)
	{
		if (Resolved) return;

		if (!SteamworksApi.Instance.InviteToCurrentLobby(friend.SteamId))
		{
			// Steam offers no callback for this, so a false return is the entire failure signal.
			DebugUtilities.PrintPeerError($"Steam: could not invite {friend.Name} ({friend.SteamId})");
			button.Text = "Failed";
			SetStatus($"Could not invite {friend.Name} — Steam refused the invite.", ColorError);
			return;
		}

		DebugUtilities.PrintPeer($"Steam: invited {friend.Name} ({friend.SteamId}) to lobby");
		_invited.Add(friend.SteamId);
		button.Text     = "Invited";
		button.Disabled = true;
		SetStatus($"Invited {friend.Name}.", ColorInfo);

		StartCooldown(friend.SteamId, button);
	}

	/// <summary>
	/// Lets an invite be sent again after a pause, for the common case of a friend who missed the first one.
	/// The timer belongs to the SceneTree, not to this node, so it keeps ticking after the dialog is closed
	/// — hence the validity checks rather than trusting it to be torn down with us.
	/// </summary>
	private void StartCooldown(ulong steamId, Button button)
	{
		GetTree().CreateTimer(InviteCooldownSeconds).Timeout += () =>
		{
			if (!IsInstanceValid(this) || Resolved) return;
			_invited.Remove(steamId);

			// The row may have been rebuilt in the meantime, taking this button with it.
			if (!IsInstanceValid(button)) return;
			button.Text     = "Invite";
			button.Disabled = false;
		};
	}

	private void OnAvatarUpdated(ulong steamId)
	{
		if (Resolved || !IsInstanceValid(this)) return;
		if (!_avatarSlots.TryGetValue(steamId, out TextureRect slot) || !IsInstanceValid(slot)) return;

		slot.Texture = SteamworksApi.Instance.SmallAvatarFor(steamId);
	}

	private void SetStatus(string message, Color color)
	{
		_statusLabel.Text = message;
		_statusLabel.AddThemeColorOverride("font_color", color);
	}
}
