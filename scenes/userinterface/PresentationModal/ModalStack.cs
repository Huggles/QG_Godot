using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Owns every open <see cref="PresentationModal"/> on this peer: the backdrop, the gutter, the
/// side-by-side row, and the priority order that decides which of them are actually on screen.
///
/// This replaced a <c>PresentationModal.Current</c> singleton. One instance per modal, one config per
/// instance, freed when it resolves — which is what makes a modal prompt recallable. The singleton had
/// to answer its pending request as cancelled every time anything else wanted the modal (a pile
/// browser, an animation), so dismissing was answering; with independent instances that whole failure
/// mode is structurally impossible, and putting a prompt aside is just <see cref="PresentationModal.Park"/>.
///
/// Priority is one MRU list, index 0 highest. <see cref="Open"/> inserts at the front so every
/// <c>Show()</c> has a visible effect; <see cref="Recall"/> fronts the parked prompt so it comes back on
/// top; parking drops a modal to the end. Anything that no longer fits stays in the list, hidden, and is
/// re-placed automatically as soon as room appears.
/// </summary>
public partial class ModalStack : Control, LoadableUI, IRecallablePrompt
{
	public static ModalStack Current;

	/// <summary>Must match %ModalRow's theme_override_constants/separation.</summary>
	private const float ModalSeparation = 40f;

	public MarginContainer ModalGutter => GetNode<MarginContainer>("%ModalGutter");
	public HBoxContainer ModalRow => GetNode<HBoxContainer>("%ModalRow");

	/// <summary>Highest priority first. Visual left-to-right order is creation order (the ModalRow child
	/// order, never re-sorted) so an existing modal does not slide sideways when a neighbour appears.</summary>
	private readonly List<PresentationModal> _modals = new();

	private InputManager.KeyClickedEventHandler _onKeyClicked;
	private InputManager _hookedInputManager;

	public override void _Ready()
	{
		if (Multiplayer.GetUniqueId() == GetMultiplayerAuthority())
			Current = this;
		LoadUI();
	}

	public void LoadUI()
	{
		// Hidden while nothing is placed, so the backdrop blocks nothing when idle.
		Visible = false;
		Resized += QueueRelayout;
		// A resolved modal frees itself only after its closing fade, which is the one transition no
		// caller reports. Without this the backdrop would stay up — blocking the board — after the last
		// modal disappeared.
		ModalRow.ChildExitingTree += OnModalRowChildExiting;
	}

	public override void _ExitTree()
	{
		UnhookEscape();
		if (Current == this) Current = null;
	}

	private void OnModalRowChildExiting(Node child) => QueueRelayout();

	// ── Entry points ─────────────────────────────────────────────────────────

	public Task<ModalResult> Show(ModalConfig config) => Open(config).Result;

	/// <summary>
	/// Show <paramref name="config"/> on a fresh instance and return it, for callers that want to hold
	/// on to the modal itself rather than only await its result.
	/// </summary>
	public PresentationModal Open(ModalConfig config)
	{
		// Already up? Toggle it shut rather than stacking an identical copy beside it. Only info modals
		// carry a key — ModalConfig.WithDedupeKey refuses anything else — because handing a second
		// caller the first request's answer would leave the host waiting on a response that never comes.
		if (config.DedupeKey != null)
		{
			PresentationModal existing = _modals.FirstOrDefault(
				modal => IsInstanceValid(modal) && modal.DedupeKey == config.DedupeKey);

			if (existing != null)
			{
				if (existing.Visible)
				{
					existing.Dismiss();
				}
				else
				{
					// Off screen only because it did not fit. Bring it forward instead of closing
					// something the player never actually got to see.
					Front(existing);
					QueueRelayout();
				}
				return existing;
			}
		}

		// A peer is only ever asked one thing at a time (see OpeningDiscard.Run). Log it and let both
		// live: with independent instances each is separately answerable, so there is no longer any
		// reason to resolve the older one — that only ever fabricated a skip for the host.
		if (config.Mode != ModalSelectionMode.Display && _modals.Any(modal => modal.IsRequest))
		{
			DebugUtilities.PrintPeerErrorRaw(
				"ModalStack: a second input request opened while one was still pending (parked or on " +
				"screen). Both stay answerable — this is a bug upstream.");
		}

		PresentationModal modal = AssetRepository.PresentationModalScenePacked.Instantiate<PresentationModal>();
		modal.Host = this;
		// Added before Prepare so its %-unique lookups resolve and the grid can be measured.
		ModalRow.AddChild(modal);
		_modals.Insert(0, modal);
		modal.Prepare(config);

		EnsureEscapeHook();
		QueueRelayout();
		return modal;
	}

	// ── Lifecycle callbacks from an instance ─────────────────────────────────

	/// <summary>
	/// A modal answered, was cancelled, or was abandoned. Freeing it is the instance's own job — it does
	/// that once its closing fade has played — so this only drops it from the priority order.
	/// </summary>
	internal void OnModalResolved(PresentationModal modal)
	{
		// Absence is normal: CancelAll empties the list before closing anything.
		_modals.Remove(modal);
		QueueRelayout();
	}

	internal void OnModalParked(PresentationModal modal)
	{
		// To the end: it is deliberately out of the way, so it must not hold space against modals the
		// player can still see.
		if (_modals.Remove(modal))
			_modals.Add(modal);
		QueueRelayout();
	}

	/// <summary>An instance finished a fade that changed whether it is on screen. Recomputes the backdrop.</summary>
	internal void OnModalVisibilityChanged() => QueueRelayout();

	internal void Front(PresentationModal modal)
	{
		if (_modals.Remove(modal))
			_modals.Insert(0, modal);
	}

	// ── Teardown ─────────────────────────────────────────────────────────────

	/// <summary>
	/// Release every open modal, for the host abandoning input (<c>NetworkApi.AbortInputRequest</c>) and
	/// for error recovery (<c>ErrorReporter.CancelPendingAwaiters</c>).
	/// </summary>
	public void CancelAll()
	{
		if (_modals.Count == 0) return;

		// Copy, clear, then iterate: closing a modal resumes the handler awaiting it, and that handler
		// may open new modals before this loop finishes. Same shape and reason as
		// PendingLocalInput.CancelAll.
		PresentationModal[] pending = _modals.ToArray();
		_modals.Clear();
		RecallablePrompts.Clear(this);
		DebugUtilities.PrintPeer($"ModalStack: cancelling {pending.Length} open modal(s)");

		foreach (PresentationModal modal in pending)
		{
			if (!IsInstanceValid(modal)) continue;
			try
			{
				modal.Dismiss();
			}
			catch (Exception e)
			{
				DebugUtilities.PrintPeerErrorRaw($"ModalStack: cancelling '{modal.Title}' failed: {e}");
			}
		}
		Visible = false;
	}

	// ── Recall seam ──────────────────────────────────────────────────────────

	public bool HasParked => _modals.Any(modal => IsInstanceValid(modal) && modal.IsParked);

	public string RecallTooltip => "Show the choice you put aside";

	public bool Recall()
	{
		// The last parked entry is the one put away most recently, since parking appends.
		PresentationModal parked = _modals.LastOrDefault(modal => IsInstanceValid(modal) && modal.IsParked);
		if (parked == null) return false;

		parked.Unpark();
		Front(parked);
		QueueRelayout();
		return true;
	}

	/// <summary>
	/// Put the on-screen input prompt aside. Used by <c>BottomLeftMenu</c> before it draws a hand onto
	/// <c>FactionHandDisplay</c>, which sits below the modal band and would otherwise be hidden behind it.
	/// </summary>
	public bool ParkTopRequest()
		=> _modals.FirstOrDefault(modal => IsInstanceValid(modal) && modal.Visible && modal.CanPark)?.Park() ?? false;

	// ── Layout ───────────────────────────────────────────────────────────────

	/// <summary>
	/// Deferred rather than immediate: a modal's width is only known after the frame's layout pass has
	/// propagated its freshly built item grid and its <c>fit_content</c> title, and a burst of changes
	/// (one modal resolving, the next opening) collapses into a single pass. Do not inline this.
	/// </summary>
	private void QueueRelayout() => Callable.From(Relayout).CallDeferred();

	private void Relayout()
	{
		if (!IsInstanceValid(this) || !IsInsideTree()) return;

		_modals.RemoveAll(modal => !IsInstanceValid(modal));

		// A modal crowded off screen is closed to make the room it was denied, rather than left lingering
		// hidden to reappear later out of nowhere. Closing mutates the list, so this is a retry loop
		// rather than a single walk; each pass either settles or closes exactly one modal, so it cannot
		// spin. Note a *new* modal is always inserted at the front and so always gets placed — the ones
		// that lose out are by construction the older, lower-priority ones.
		int safety = _modals.Count + 1;
		while (safety-- > 0)
		{
			PlaceModals();

			PresentationModal sacrifice = LowestClosableCrowdedOut();
			if (sacrifice == null) break;

			DebugUtilities.PrintPeer(
				$"ModalStack: closing '{sacrifice.Title}' — no room for it beside the modals above it.");
			sacrifice.DismissForSpace();
		}

		foreach (PresentationModal held in _modals)
		{
			if (!held.Visible && !held.IsParked)
			{
				DebugUtilities.PrintPeerFinest(
					$"ModalStack: '{held.Title}' does not fit and must not be closed — held in the stack until room appears.");
			}
		}

		// Off the row's actual children rather than off _modals: a modal that has just been answered is
		// already out of the priority order but still on screen playing its closing fade, and hiding the
		// host would cut that fade off.
		bool anythingOnScreen = false;
		foreach (Node child in ModalRow.GetChildren())
		{
			if (child is Control { Visible: true })
			{
				anythingOnScreen = true;
				break;
			}
		}
		Visible = anythingOnScreen;

		// Clear rather than Set(null): the slot may belong to an open card prompt, and this stack must
		// only ever release its own claim on it.
		if (HasParked) RecallablePrompts.Set(this);
		else RecallablePrompts.Clear(this);
	}

	/// <summary>Walk the priority order, placing what fits and un-placing what does not.</summary>
	private void PlaceModals()
	{
		float available = AvailableWidth();
		float used = 0f;
		bool first = true;

		foreach (PresentationModal modal in _modals)
		{
			if (modal.IsParked)
			{
				modal.SetPlaced(false);
				continue;
			}

			float width = modal.GetCombinedMinimumSize().X;
			float needed = used + width + (first ? 0f : ModalSeparation);

			// The highest-priority modal is placed even when it alone overflows: clipping one modal
			// beats showing an empty screen, and a 7-column card grid has always behaved that way.
			if (first || needed <= available)
			{
				modal.SetPlaced(true);
				used = needed;
				first = false;
			}
			else
			{
				modal.SetPlaced(false);
			}
		}
	}

	/// <summary>
	/// The lowest-priority modal that was crowded off screen and is safe to close, or null when every
	/// crowded-out modal must be kept.
	///
	/// Info modals only. Closing an input request resolves it as cancelled, which <c>BroadCast</c> turns
	/// into a <c>StepSkippedException</c> — silently abandoning a card step and answering the host on the
	/// player's behalf. The parked case is sharper still: parking appends, so a prompt the player
	/// deliberately set aside is *always* lowest on the priority list, and a naive "close the lowest"
	/// rule would make it the very first casualty. Both are left hidden and return when room appears.
	/// </summary>
	private PresentationModal LowestClosableCrowdedOut()
	{
		for (int i = _modals.Count - 1; i >= 0; i--)
		{
			PresentationModal modal = _modals[i];
			if (modal.IsRequest || modal.IsParked) continue;
			// On screen, so it found room and is not a casualty.
			if (modal.Visible) continue;
			return modal;
		}
		return null;
	}

	/// <summary>
	/// Read off the viewport and the gutter's own margins rather than the row's measured size, so the
	/// first relayout is correct even though the host is still hidden and therefore unlaid-out.
	/// </summary>
	private float AvailableWidth()
	{
		float gutter = ModalGutter.GetThemeConstant("margin_left") + ModalGutter.GetThemeConstant("margin_right");
		return Mathf.Max(0f, GetViewportRect().Size.X - gutter);
	}

	// ── Escape arbitration ───────────────────────────────────────────────────

	/// <summary>
	/// One subscription for the whole stack — with N instances, a per-modal hook would have every one of
	/// them react to the same key press. Re-checked on every <see cref="Open"/> because
	/// <c>InputManager.Current</c> may be null when the first modal appears, and may be a different node
	/// after a scene change.
	/// </summary>
	private void EnsureEscapeHook()
	{
		InputManager inputManager = InputManager.Current;
		if (inputManager == null || ReferenceEquals(inputManager, _hookedInputManager)) return;

		UnhookEscape();
		_onKeyClicked = key =>
		{
			if (key.Keycode == Key.Escape) EscapeTarget()?.HandleEscape();
		};
		inputManager.KeyClicked += _onKeyClicked;
		_hookedInputManager = inputManager;
	}

	private void UnhookEscape()
	{
		if (_onKeyClicked != null && _hookedInputManager != null && IsInstanceValid(_hookedInputManager))
			_hookedInputManager.KeyClicked -= _onKeyClicked;
		_onKeyClicked = null;
		_hookedInputManager = null;
	}

	/// <summary>
	/// The highest-priority placed input request, or the highest-priority placed info modal when no
	/// request is on screen — an info modal must never take Escape away from a pending request. Parked
	/// and overflow-hidden instances are not <c>Visible</c> and so are never candidates.
	/// </summary>
	private PresentationModal EscapeTarget()
		=> _modals.FirstOrDefault(modal => IsInstanceValid(modal) && modal.Visible && modal.IsRequest)
		?? _modals.FirstOrDefault(modal => IsInstanceValid(modal) && modal.Visible);

	/// <summary>
	/// Whether this stack would act on an Escape. Read by <see cref="GameMenuTrigger"/> so the
	/// in-game menu never opens over a modal that is already waiting for that key.
	/// </summary>
	public bool HasEscapeTarget => EscapeTarget() != null;
}
