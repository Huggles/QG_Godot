using System;
using System.Collections.Generic;

/// <summary>
/// Registry of board selections this peer is currently waiting on (SelectCountry / SelectUnit /
/// SelectBattleTarget). Each registers a canceller that completes its TaskCompletionSource with the
/// handler's existing "skipped" sentinel (-1 for country/unit, null for battle target), which the
/// InputRequest handlers already map onto <c>WasSkipped = true</c>.
///
/// Needed because those TCS live on the controlling CLIENT and are only ever completed by a
/// CountryClicked / UnitClicked / SelectionSkipped signal. When the server aborts a step after a
/// failure, nothing else would ever complete them and the client would sit on a dead prompt.
/// </summary>
public static class PendingLocalInput
{
    private static readonly List<Action> _cancellers = new();

    /// <summary>Register a canceller. Returns a token that unregisters it when disposed.</summary>
    public static IDisposable Register(Action cancel)
    {
        if (cancel == null) return new Registration(null);
        _cancellers.Add(cancel);
        return new Registration(cancel);
    }

    /// <summary>Complete every pending selection as skipped.</summary>
    public static void CancelAll()
    {
        if (_cancellers.Count == 0) return;

        // Copy first: a canceller completing its TCS can synchronously run the continuation, which
        // disposes its own registration and mutates the list.
        Action[] pending = _cancellers.ToArray();
        _cancellers.Clear();

        DebugUtilities.PrintPeer($"PendingLocalInput: cancelling {pending.Length} open selection(s)");
        foreach (Action cancel in pending)
        {
            try { cancel(); }
            catch (Exception e) { DebugUtilities.PrintPeerErrorRaw($"PendingLocalInput cancel failed: {e}"); }
        }
    }

    private sealed class Registration : IDisposable
    {
        private Action _cancel;

        public Registration(Action cancel) => _cancel = cancel;

        public void Dispose()
        {
            if (_cancel == null) return;
            _cancellers.Remove(_cancel);
            _cancel = null;
        }
    }
}
