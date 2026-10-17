using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Central registry for all active modifiers. Modifiers are registered when their
/// source card is played to the status zone and unregistered when it is discarded.
/// Query with GetAll&lt;T&gt;() from any event or calculation that needs to apply modifiers.
/// </summary>
public static class ModifierRegistry
{
    private static readonly List<IModifier> _modifiers = new();

    /// <summary>How many IStatusIgnoreModifier cards are registered, kept so BoardState.Modifiers can skip its filter in one comparison.</summary>
    private static int _statusIgnoreCount;

    public static bool HasStatusIgnoreModifiers => _statusIgnoreCount > 0;

    public static void Register(IModifier modifier)
    {
        if (_modifiers.Contains(modifier)) return;
        _modifiers.Add(modifier);
        if (modifier is IStatusIgnoreModifier) _statusIgnoreCount++;
    }

    public static void Unregister(IModifier modifier)
    {
        if (_modifiers.Remove(modifier) && modifier is IStatusIgnoreModifier) _statusIgnoreCount--;
    }

    /// <summary>
    /// Drop every modifier. The backing list is static, so without this a second game started in the
    /// same process would inherit the previous game's modifiers. Called from game-mode Init().
    /// </summary>
    public static void Clear()
    {
        _modifiers.Clear();
        _statusIgnoreCount = 0;
    }

    public static IEnumerable<T> GetAll<T>() where T : IModifier => _modifiers.OfType<T>();

    /// <summary>The live modifiers in registration order, copied for a forked board.</summary>
    public static List<IModifier> Snapshot() => new(_modifiers);

    /// <summary>
    /// Whether any modifier is registered that a save-game replay could not rebuild. See
    /// <see cref="IUnsavedModifier"/> — GameFlow.CanSave refuses to save while one is live.
    /// </summary>
    public static bool HasUnsavedModifiers => _modifiers.OfType<IUnsavedModifier>().Any();
}
