/// <summary>
/// The open card prompt, as something the recall button can re-draw. Stateless — the prompt itself
/// lives in <see cref="InputManager.CurrentCardPrompt"/>, and re-drawing is
/// <see cref="InputManager.ShowCurrentCardPrompt"/>.
/// </summary>
public sealed class CardPromptRecall : IRecallablePrompt
{
    public static readonly CardPromptRecall Instance = new();

    private CardPromptRecall() { }

    public string RecallTooltip => "Show the cards you are being asked to choose from";

    /// <summary>Browsing another hand is the one thing that draws over an open card prompt.</summary>
    public bool IsOnScreen => !HandBrowsing.IsActive;

    public bool Recall() => InputManager.ShowCurrentCardPrompt();
}
