using System.Threading.Tasks;

/// <summary>
/// Puts a commander message on screen and holds the game there until the player presses CONTINUE.
///
/// A blocking animation, which is exactly what <see cref="PresentationEvent"/> is built to carry —
/// its ApplyInternal parks on the animation queue until this returns, so the turn loop waits here the
/// same way it waits on an InputRequest. That is the whole mechanism behind "the tutorial does not
/// move on until you have read this".
///
/// A TaskCompletionSource rather than <c>await ToSignal(...)</c>: the continue press arrives on the
/// EventBus rather than on a signal of the node's (see CommanderMessage's own remarks on why), and
/// the project's preference is a TCS whenever a signal has to become one awaitable. Registered with
/// <see cref="PendingLocalInput"/> for the reason CliInputProvider registers its prompt — an error
/// reported while a message is up would otherwise leave this parked forever, and the error popup's
/// Continue would deadlock against it.
/// </summary>
public class ShowCommanderMessageAnimation : ChangeEventAnimation
{
    private readonly string _text;
    private readonly bool _wait;
    private readonly TutorialArrowData _arrow;

    public ShowCommanderMessageAnimation(string text, bool wait = true, TutorialArrowData arrow = null)
    {
        _text = text;
        _wait = wait;
        _arrow = arrow;
    }

    protected override async Task AnimateForTargetFaction()
    {
        // A freed node leaves a non-null C# wrapper that throws on access, so IsInstanceValid rather
        // than a null check — the rule for every static holding a Godot node in this project.
        if (!Godot.GodotObject.IsInstanceValid(CommanderMessage.Current))
        {
            // No HUD: a headless run, or a peer whose interface has not been built. Log the line so a
            // scripted run still shows what the player would have been told, and never block.
            DebugUtilities.PrintPeer($"[commander] {_text}");
            return;
        }

        CommanderMessage commander = CommanderMessage.Current;
        commander.ShowMessage(_text);
        ShowArrow();

        // A message that does not wait leaves its arrow up, and the NEXT message replaces or clears
        // it. That is the point of a non-waiting message — it narrates over something already
        // happening — and taking the arrow away a frame after raising it would be pure flicker.
        if (!_wait) return;

        TaskCompletionSource<bool> pressed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnContinued() => pressed.TrySetResult(true);

        EventBus.Instance.CommanderMessageContinued += OnContinued;
        try
        {
            using (PendingLocalInput.Register(() => pressed.TrySetResult(true)))
                await pressed.Task;
        }
        finally
        {
            EventBus.Instance.CommanderMessageContinued -= OnContinued;
            // Guarded again: an error sweep between the show above and here can tear the HUD down.
            if (Godot.GodotObject.IsInstanceValid(commander)) commander.HideMessage();
            // In the finally with the panel, and for the same reason: an arrow left pointing at the
            // hand after the commander has gone is the one piece of this that outlives its own message.
            if (Godot.GodotObject.IsInstanceValid(TutorialArrow.Current)) TutorialArrow.Current.Clear();
        }
    }

    /// <summary>
    /// Raise the arrow, or take down whatever the previous message left up.
    ///
    /// A message with no arrow actively CLEARS one rather than leaving it alone: an arrow that
    /// outlives the sentence it belonged to is pointing at the wrong thing, which is worse than not
    /// pointing at all.
    /// </summary>
    private void ShowArrow()
    {
        if (!Godot.GodotObject.IsInstanceValid(TutorialArrow.Current)) return;

        if (_arrow == null) { TutorialArrow.Current.Clear(); return; }

        DebugUtilities.PrintPeer($"[commander] arrow {_arrow}");
        TutorialArrow.Current.PointAt(_arrow);
    }
}
