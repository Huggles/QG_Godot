using System;

public static class ITargetSetProviderExtensions
{
    /// <summary>
    /// <see cref="ITargetSetProvider.Targets"/>, but a provider that throws contributes nothing
    /// instead of taking down the prompt it is riding on.
    ///
    /// Worth having as the only sanctioned entry point rather than a try/catch at each call site: a
    /// target expression is written to run inside a live step, so evaluating it a moment early can
    /// legitimately throw — a reaction trigger that is null because no window is open, a pool read
    /// with nothing in it. The preview is advisory, so the honest failure mode is a dark board, never
    /// a broken prompt.
    ///
    /// Null-tolerant on the provider itself, so a caller resolving one out of a lookup
    /// (<c>CardState.ForId(id)?.CardLogic</c>) needs no separate guard.
    /// </summary>
    public static TargetSet TargetsOrNone(this ITargetSetProvider provider)
    {
        if (provider == null) return TargetSet.None;

        try
        {
            return provider.Targets() ?? TargetSet.None;
        }
        catch (Exception e)
        {
            DebugUtilities.PrintPeer($"Targets() failed for {provider.GetType().Name}: {e.Message}");
            return TargetSet.None;
        }
    }
}
