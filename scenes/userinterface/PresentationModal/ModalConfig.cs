using System.Collections.Generic;

public enum ModalSelectionMode
{
    Display,
    MultiSelect,
    Reorder
}

public class ModalConfig
{
    public string Title { get; private set; }
    public List<PresentationItem> Items { get; private set; }
    public ModalSelectionMode Mode { get; private set; }
    public int MinSelections { get; private set; }
    public int MaxSelections { get; private set; }   // -1 = unlimited
    public string ApplyLabel { get; private set; } = "Apply";
    public string CancelLabel { get; private set; } = "Cancel";

    public bool AutoDismiss { get; private set; } = false;

    /// <summary>
    /// Whether the items burn away once the modal is on screen, the modal closing when the last of them
    /// is gone. See <see cref="WithBurnAway"/>.
    /// </summary>
    public bool BurnAway { get; private set; } = false;

    /// <summary>This modal's identity, or null when it does not deduplicate. See <see cref="WithDedupeKey"/>.</summary>
    public string DedupeKey { get; private set; }

    public bool ShowApplyButton => Mode != ModalSelectionMode.Display;

    /// <summary>
    /// The "×" in the title bar. Info modals only — a prompt never offers one.
    ///
    /// A mandatory prompt never had one (there is no answer to give). An optional one no longer needs it:
    /// "I decline" is already Apply with nothing selected, which both discard paths treat identically to a
    /// cancel (<c>result.WasCancelled ? new List&lt;int&gt;() : result.SelectedItems</c>). Two buttons for
    /// one outcome only invited the player to wonder which was which. Declining is now always Apply, and
    /// getting a prompt off the screen is always Hide.
    ///
    /// <see cref="ModalResult.WasCancelled"/> is still produced for prompts — by
    /// <c>PresentationModal.Dismiss</c> on the error-recovery and stack-teardown paths — so callers must
    /// keep handling it.
    /// </summary>
    public bool ShowCancelButton => Mode == ModalSelectionMode.Display && !AutoDismiss;

    /// <summary>
    /// Whether this modal offers the "Hide" button, which puts the prompt aside without answering it so
    /// the player can look at the board and bring it back from the bottom-left menu.
    ///
    /// A selection prompt is exactly what needs it: no prompt carries a Cancel button, so this is the only
    /// way to get one off the screen without answering it. An info modal has nothing to come back to, and
    /// an auto-dismissing one is gone before you could recall it.
    ///
    /// Together with <see cref="ShowCancelButton"/> this gives the title bar exactly one button: "−" on a
    /// prompt, "×" on an info modal.
    /// </summary>
    public bool ShowParkButton => Mode != ModalSelectionMode.Display && !AutoDismiss;

    private ModalConfig() { }

    /// <summary>Show-only: no selection, Cancel="Close" button, no Apply button.</summary>
    public static ModalConfig Display(string title, List<PresentationItem> items)
        => new ModalConfig { Title = title, Items = items, Mode = ModalSelectionMode.Display, MinSelections = 0, MaxSelections = 0, CancelLabel = "Close" };

    /// <summary>Pick exactly one item. Cancel hidden when required=true.</summary>
    public static ModalConfig SelectOne(string title, List<PresentationItem> items, bool required = true)
        => new ModalConfig { Title = title, Items = items, Mode = ModalSelectionMode.MultiSelect, MinSelections = required ? 1 : 0, MaxSelections = 1 };

    /// <summary>Pick 0–max items. Cancel hidden when min > 0.</summary>
    public static ModalConfig SelectMany(string title, List<PresentationItem> items, int min = 0, int max = -1)
        => new ModalConfig { Title = title, Items = items, Mode = ModalSelectionMode.MultiSelect, MinSelections = min, MaxSelections = max };

    /// <summary>
    /// Pick exactly <paramref name="count"/> items — no more, no fewer, and no Cancel.
    ///
    /// A mandatory discard used SelectMany(min: count), which leaves MaxSelections unlimited: the
    /// player could select more than they were asked for and every one of them was discarded. The CLI
    /// path (InputRequestSpec) has always clamped both ends; this is the GUI catching up.
    /// </summary>
    public static ModalConfig SelectExactly(string title, List<PresentationItem> items, int count)
        => new ModalConfig { Title = title, Items = items, Mode = ModalSelectionMode.MultiSelect, MinSelections = count, MaxSelections = count };

    /// <summary>Place all items in a chosen order. Apply enabled only when all items are placed. No Cancel.</summary>
    public static ModalConfig Reorder(string title, List<PresentationItem> items)
        => new ModalConfig { Title = title, Items = items, Mode = ModalSelectionMode.Reorder, MinSelections = items.Count, MaxSelections = items.Count };

    /// <summary>
    /// Give this modal an identity, so pressing the button that opens it again while it is still up
    /// toggles it shut rather than stacking an identical copy beside it. Two configs carrying the same
    /// key are the same modal.
    ///
    /// Info modals only, and enforced here rather than left to the stack: deduplicating an input request
    /// would hand the second caller the *first* request's answer, and the host would sit waiting on a
    /// response for its own request that never comes.
    /// </summary>
    public ModalConfig WithDedupeKey(string key)
    {
        if (Mode != ModalSelectionMode.Display)
            throw new System.InvalidOperationException(
                $"Only an info modal may carry a dedupe key; this one is {Mode}.");
        DedupeKey = key;
        return this;
    }

    public ModalConfig WithApplyLabel(string label) { ApplyLabel = label; return this; }
    public ModalConfig WithCancelLabel(string label) { CancelLabel = label; return this; }
    public ModalConfig WithAutoDismiss()
    {
        if (MinSelections > 0)
            throw new System.InvalidOperationException("Cannot auto-dismiss a modal that requires selection.");
        AutoDismiss = true;
        return this;
    }

    /// <summary>
    /// Burn the items away as soon as the modal has faded in, and close it the moment the last of them
    /// is gone. For a discard: the player sees which cards went, and then watches them go.
    ///
    /// This is a form of auto-dismiss rather than an addition to it — the burn replaces the timer, since
    /// the modal now has a natural moment to close — so it carries the same restrictions, and a player
    /// who has turned auto-dismiss off keeps their Close button and sees no burn. Burning to a modal
    /// that then sat there empty waiting to be closed would be the worse reading of that setting.
    ///
    /// Info modals only, for the same reason auto-dismiss is: there is no answering a prompt whose
    /// options have burnt away.
    /// </summary>
    public ModalConfig WithBurnAway()
    {
        if (Mode != ModalSelectionMode.Display)
            throw new System.InvalidOperationException(
                $"Only an info modal may burn its items away; this one is {Mode}.");
        WithAutoDismiss();
        BurnAway = true;
        return this;
    }
}
