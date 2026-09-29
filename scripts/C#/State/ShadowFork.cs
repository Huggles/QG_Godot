using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// A debug check that a fork changes exactly as the live board does. With <c>shadow_forks=true</c> on
/// the command line, every ChangeEvent the host applies is also applied to a fork of the board as it
/// stood just before: the fork is mutated, its tags are derived, and a full <see cref="BoardState.Digest"/>
/// is compared with the live board's once that has been recalculated. A difference prints one
/// SHADOW_MISMATCH line naming the event and the first object that disagrees; play carries on.
///
/// Off by default and free when off. It proves Mutate and the tag pass on real games, which the
/// bot's projections depend on and nothing else exercises.
/// </summary>
public static class ShadowFork
{
    private static readonly bool Enabled = CliArgs.GetBool("shadow_forks");

    public static int Checked { get; private set; }
    public static int Mismatched { get; private set; }

    public sealed class Pending
    {
        public BoardState Board;
        public Exception Error;
    }

    /// <summary>Fork the live board and mutate the fork. Called after the event's choices are resolved, before the live mutation.</summary>
    public static Pending Take(ChangeEvent changeEvent)
    {
        if (!Enabled || MultiplayerSession.Instance?.Multiplayer.IsServer() != true) return null;

        Pending pending = new() { Board = BoardState.Live.Fork() };
        try { changeEvent.Mutate(pending.Board); }
        catch (Exception e) { pending.Error = e; }
        return pending;
    }

    /// <summary>Derive the fork's tags the way the live board's just were, and compare the two.</summary>
    public static void Compare(ChangeEvent changeEvent, Pending pending)
    {
        if (pending == null) return;
        if (++Checked % 250 == 0)
            GD.Print($"SHADOW_PROGRESS {Checked} checked, {Mismatched} mismatched");

        // An event a fork refuses on purpose (a Bulletin creates a card) is not a disagreement.
        if (pending.Error is NotSupportedException) return;
        if (pending.Error != null)
        {
            Report(changeEvent, $"the fork threw {pending.Error.GetType().Name}: {pending.Error.Message}");
            return;
        }

        if (GameStateCalculator.Enabled)
            GameStateCalculator.CalculateAll(changeEvent.RecalcScope, GameSituation.Live.WithBoard(pending.Board));

        List<string> live = BoardState.Live.Digest();
        List<string> fork = pending.Board.Digest();
        for (int i = 0; i < Math.Max(live.Count, fork.Count); i++)
        {
            string l = i < live.Count ? live[i] : "<none>";
            string f = i < fork.Count ? fork[i] : "<none>";
            if (l == f) continue;
            Report(changeEvent, $"live `{l}` fork `{f}`");
            return;
        }
    }

    private static void Report(ChangeEvent changeEvent, string detail)
    {
        Mismatched++;
        GD.Print($"SHADOW_MISMATCH {changeEvent.ScriptName} #{changeEvent.Id}: {detail}");
    }
}
