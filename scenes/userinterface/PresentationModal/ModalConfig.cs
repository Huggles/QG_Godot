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

    public bool ShowApplyButton => Mode != ModalSelectionMode.Display;
    public bool ShowCancelButton => MinSelections == 0 && !AutoDismiss;

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

    /// <summary>Place all items in a chosen order. Apply enabled only when all items are placed. No Cancel.</summary>
    public static ModalConfig Reorder(string title, List<PresentationItem> items)
        => new ModalConfig { Title = title, Items = items, Mode = ModalSelectionMode.Reorder, MinSelections = items.Count, MaxSelections = items.Count };

    public ModalConfig WithApplyLabel(string label) { ApplyLabel = label; return this; }
    public ModalConfig WithCancelLabel(string label) { CancelLabel = label; return this; }
    public ModalConfig WithAutoDismiss()
    {
        if (MinSelections > 0)
            throw new System.InvalidOperationException("Cannot auto-dismiss a modal that requires selection.");
        AutoDismiss = true;
        return this;
    }
}
