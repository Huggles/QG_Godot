using System.Threading.Tasks;

// ─────────────────────────────────────────────────────────────────────────────
// Input seam.
//
// Sibling of the presentation seam in PresentationSinks.cs, and for the same reason: the
// authoritative loop must be able to run in a process with no window, rendering, or UI.
//
// InputRequest.Execute() used to call Handle() directly, and every one of the 12 Handle()
// implementations reaches into a UI singleton (PlayerScene.Current.InputManager,
// ModalStack.Current, FactionHandDisplay.Current, SelectionSkipButton.Current). None of those
// exist headless, so a headless process that actually controls a faction NREs on the first prompt.
//
// Execute() now routes through InputServices.Provider instead. The Godot provider is a pass-through
// to the existing Handle(), so the GUI path is bit-for-bit unchanged; scripted providers answer from
// somewhere else entirely.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Answers an <see cref="InputRequest"/> by filling its <c>Response*Ids</c> / <c>WasSkipped</c>
/// fields. Returns when the answer is complete.
/// </summary>
public interface IInputProvider
{
    Task Resolve(InputRequest request);
}

/// <summary>The GUI path: run the request's own <c>Handle()</c>, exactly as before.</summary>
public sealed class GodotInputProvider : IInputProvider
{
    public Task Resolve(InputRequest request) => request.Handle();
}

/// <summary>
/// Passes on everything. Not a toy: it exercises the whole turn loop end to end with no input at
/// all, which is the cheapest possible smoke test of a rules change and the fallback when a CLI
/// session ends but the game is still running.
///
/// Uses each request's own pass idiom rather than blanket-setting WasSkipped — see
/// <see cref="InputRequestSpec"/> for why that distinction is load-bearing.
/// </summary>
public sealed class AutoPassInputProvider : IInputProvider
{
    public Task Resolve(InputRequest request)
    {
        InputRequestSpec.For(request).ApplyPass(request);
        return Task.CompletedTask;
    }
}
