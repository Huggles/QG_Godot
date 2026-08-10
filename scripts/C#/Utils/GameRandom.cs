using System;
using System.Collections.Generic;

/// <summary>
/// The game's single source of randomness, so a run can be reproduced from a seed.
///
/// Before this, randomness came from unseeded `new Random()` instances scattered across the deck
/// shuffle, the starting-VP roll, and two card scripts — which made a headless replay impossible to
/// pin down.
///
/// Deliberately NOT thread-safe and not meant to be: every draw happens on the Godot main thread as
/// part of applying a ChangeEvent. It is also host-only by contract — a client that draws would
/// diverge from the host with nothing to catch it, since MultiplayerGameState.ComputeHash covers deck
/// *counts* but not deck order.
/// </summary>
public static class GameRandom
{
    private static Random _random = new();

    /// <summary>The seed in force. Printed at CLI start so an unseeded run can still be re-pinned.</summary>
    public static int Seed { get; private set; }

    /// <summary>Draws taken since the last <see cref="Initialize"/> — a cheap divergence fingerprint.</summary>
    public static int DrawCount { get; private set; }

    /// <summary>
    /// (Re)seed. Called once per game, before any state is built — a second game in the same process
    /// must not inherit the first game's stream.
    /// </summary>
    public static void Initialize(int seed)
    {
        Seed = seed;
        DrawCount = 0;
        _random = new Random(seed);
    }

    /// <summary>The underlying generator, for callers that need a <see cref="Random"/> (e.g. Shuffle).</summary>
    public static Random Raw { get { DrawCount++; return _random; } }

    public static int Next(int maxExclusive) { DrawCount++; return _random.Next(maxExclusive); }

    /// <summary>
    /// Fisher-Yates, in place. Here rather than as a list extension so every draw is counted and
    /// every shuffle goes through the seeded generator — an unseeded shuffle is the one form of
    /// nondeterminism that survives a replay unnoticed.
    /// </summary>
    public static void Shuffle<T>(IList<T> list)
    {
        WarnIfNotServer("Shuffle");
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    /// <summary>
    /// Randomness is host-only by contract: authoritative state is computed on the host and
    /// replicated, and MultiplayerGameState.ComputeHash covers deck *counts* but not deck order —
    /// so a client that draws would diverge with nothing to catch it until several turns later.
    ///
    /// Warns rather than throws: this is a guard against a future mistake, and turning a live game
    /// into a crash would be a worse failure than the divergence it is warning about. Goes to stderr,
    /// which stays visible even when CLI mode mutes the normal console.
    /// </summary>
    private static void WarnIfNotServer(string site)
    {
        if (MultiplayerSession.Instance?.Multiplayer?.IsServer() != false) return;
        DebugUtilities.PrintPeerErrorRaw(
            $"GameRandom.{site} called on a CLIENT. Randomness must be host-only and replicated — " +
            "this will silently diverge deck order from the host.");
    }

    public static int Range(int minInclusive, int maxInclusive)
    {
        DrawCount++;
        return _random.Next(minInclusive, maxInclusive + 1);
    }
}
