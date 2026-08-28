using System.Collections.Generic;
using System.Threading.Tasks;

// ─────────────────────────────────────────────────────────────────────────────
// Presentation seam.
//
// The simulation runs authoritatively on the host and (on a dedicated server) with no window,
// rendering, audio, or UI. To keep logic free of null-derefs and camera/scene access in that
// mode, all presentation touchpoints on the logic path go through these interfaces instead of
// reaching into scene-tree singletons directly.
//
// PresentationServices resolves each interface once, lazily, to a Godot implementation (GUI
// client / GUI host) or a no-op implementation (headless server), keyed off GameContext.IsHeadless.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Enqueue/await board animations. Backed by <c>AnimationQueue</c> on a GUI process.</summary>
public interface IAnimationSink
{
    /// <summary>Queue an animation. Fire-and-forget at the call sites; the returned task completes when the animation does.</summary>
    Task Enqueue(ChangeEventAnimation animation);

    /// <summary>Ensure the queue is draining (no-op if empty/already running). Awaited by ChangeEvent.Apply.</summary>
    Task Start();
}

/// <summary>Player-facing messages and modals. Backed by <c>PlayerActionLabel</c>/<c>ModalStack</c> on a GUI process.</summary>
public interface INotificationSink
{
    void ShowActionText(string text, Faction faction = (Faction)(-1));
    void ShowActionText(string text, int duration, Faction faction = (Faction)(-1));
    void HideActionText();

    /// <summary>Show an info modal and complete when it is dismissed. Never displays on headless.</summary>
    Task ShowModal(List<PresentationItem> items, string title);

    /// <summary>True when the local player controls <paramref name="faction"/>. Always false headless (server controls no faction).</summary>
    bool LocalPlayerControls(Faction faction);
}

/// <summary>Spawns the visual world (country/unit <c>Node2D</c>s). No-op on headless — authoritative state is built separately.</summary>
public interface IWorldPresenter
{
    void SpawnCountries();
    void SpawnUnits();
}

// ── Godot (GUI) implementations ──────────────────────────────────────────────

public sealed class GodotAnimationSink : IAnimationSink
{
    public Task Enqueue(ChangeEventAnimation animation) => AnimationQueue.Instance.Enqueue(animation);
    public Task Start() => AnimationQueue.Instance.Start();
}

public sealed class GodotNotificationSink : INotificationSink
{
    public void ShowActionText(string text, Faction faction = (Faction)(-1)) => PlayerActionLabel.ShowText(text, faction);
    public void ShowActionText(string text, int duration, Faction faction = (Faction)(-1)) => PlayerActionLabel.ShowText(text, duration, faction);
    public void HideActionText() => PlayerActionLabel.HideText();

    public Task ShowModal(List<PresentationItem> items, string title)
        => ModalStack.Current.Show(ModalConfig.Display(title, items).WithAutoDismiss());

    public bool LocalPlayerControls(Faction faction)
        => PlayerScene.Current?.ControlledFactions.Contains(faction) ?? false;
}

public sealed class GodotWorldPresenter : IWorldPresenter
{
    private static MultiplayerGameState GameState => MultiplayerSession.Instance.GameState;

    public void SpawnCountries()
    {
        foreach (CountryState countryState in GameState.CountryStates)
            CountryScene.SpawnCountry(countryState.Id);
    }

    public void SpawnUnits()
    {
        foreach (UnitState unitState in GameState.UnitStates)
            UnitScene.SpawnUnit(unitState.Id);
    }
}

// ── Headless (no-op) implementations ─────────────────────────────────────────

public sealed class NullAnimationSink : IAnimationSink
{
    public Task Enqueue(ChangeEventAnimation animation) => Task.CompletedTask;
    public Task Start() => Task.CompletedTask;
}

public sealed class NullNotificationSink : INotificationSink
{
    public void ShowActionText(string text, Faction faction = (Faction)(-1)) { }
    public void ShowActionText(string text, int duration, Faction faction = (Faction)(-1)) { }
    public void HideActionText() { }
    public Task ShowModal(List<PresentationItem> items, string title) => Task.CompletedTask;
    public bool LocalPlayerControls(Faction faction) => false;
}

public sealed class NullWorldPresenter : IWorldPresenter
{
    public void SpawnCountries() { }
    public void SpawnUnits() { }
}

// ── Save-game replay ─────────────────────────────────────────────────────────

/// <summary>
/// Wraps the real notification sink while a save is being restored: silences everything the player
/// would see or have to dismiss, but forwards <see cref="LocalPlayerControls"/> unchanged.
///
/// That one method is why this exists instead of just using <see cref="NullNotificationSink"/>. It is
/// not presentation plumbing — <c>GameAPI.DrawCards</c> and <c>CardState.IsFaceVisibleToLocalPlayer</c>
/// both branch on it, so answering a flat false during a replay would make the restored game take
/// different paths from the one that was saved.
/// </summary>
public sealed class ReplayNotificationSink : INotificationSink
{
    public INotificationSink Inner { get; }

    public ReplayNotificationSink(INotificationSink inner) => Inner = inner;

    public void ShowActionText(string text, Faction faction = (Faction)(-1)) { }
    public void ShowActionText(string text, int duration, Faction faction = (Faction)(-1)) { }
    public void HideActionText() { }

    /// <summary>Never shown, and never awaited — a restore must not stop for a modal per drawn card.</summary>
    public Task ShowModal(List<PresentationItem> items, string title) => Task.CompletedTask;

    public bool LocalPlayerControls(Faction faction) => Inner.LocalPlayerControls(faction);
}
