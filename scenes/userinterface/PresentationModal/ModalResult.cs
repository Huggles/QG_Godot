using System.Collections.Generic;

public class ModalResult
{
    public List<int> SelectedItems { get; }
    public bool WasCancelled { get; }

    public static ModalResult Cancelled => new ModalResult(new List<int>(), wasCancelled: true);

    public ModalResult(List<int> selectedItems, bool wasCancelled = false)
    {
        SelectedItems = selectedItems;
        WasCancelled = wasCancelled;
    }
}
