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

    public static void Register(IModifier modifier)
    {
        if (!_modifiers.Contains(modifier))
            _modifiers.Add(modifier);
    }

    public static void Unregister(IModifier modifier) => _modifiers.Remove(modifier);

    /// <summary>
    /// Drop every modifier. The backing list is static, so without this a second game started in the
    /// same process would inherit the previous game's modifiers. Called from game-mode Init().
    /// </summary>
    public static void Clear() => _modifiers.Clear();

    public static IEnumerable<T> GetAll<T>() where T : IModifier => _modifiers.OfType<T>();

    /// <summary>
    /// Whether any modifier is registered that a save-game replay could not rebuild. See
    /// <see cref="IUnsavedModifier"/> — GameFlow.CanSave refuses to save while one is live.
    /// </summary>
    public static bool HasUnsavedModifiers => _modifiers.OfType<IUnsavedModifier>().Any();
}
