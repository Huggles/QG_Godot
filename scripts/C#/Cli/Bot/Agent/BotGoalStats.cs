using System;

/// <summary>
/// Per-goal-kind counters for one run, the goal-layer twin of <see cref="BotRuleStats"/>.
///
/// **This is the deliverable, not a debug aid.** Nothing consumes the agenda yet, so no game result moves
/// when a goal is valued wrongly — which means these counters and the per-prompt trace are the ONLY
/// things standing between a goal model that describes the board and one that quietly does not. A
/// proposer that never fires, or one that fires on every prompt of every game, is a bug that would
/// otherwise be invisible until something started depending on it.
///
/// Counted per agenda build, which is once per prompt, so the numbers are on the same footing as the
/// prompt counts already emitted beside them.
/// </summary>
public sealed class BotGoalStats
{
    private static readonly GoalKind[] AllKinds = (GoalKind[])Enum.GetValues(typeof(GoalKind));

    private readonly int[] _proposed;
    private readonly int[] _topped;
    private readonly int[] _agendasContaining;

    /// <summary>Agendas built — one per prompt answered while an agent was configured.</summary>
    public int AgendasBuilt { get; private set; }

    /// <summary>Agendas that wanted nothing at all. A high count here is the first thing worth explaining.</summary>
    public int EmptyAgendas { get; private set; }

    public BotGoalStats()
    {
        int size = AllKinds.Length;
        _proposed = new int[size];
        _topped = new int[size];
        _agendasContaining = new int[size];
    }

    public static GoalKind[] Kinds => AllKinds;

    /// <summary>Total goals of this kind proposed across the run — several per agenda is normal.</summary>
    public int Proposed(GoalKind kind) => _proposed[(int)kind];

    /// <summary>Agendas in which this kind was the single highest-valued goal. What actually drove play.</summary>
    public int Topped(GoalKind kind) => _topped[(int)kind];

    /// <summary>Agendas containing at least one goal of this kind, however far down.</summary>
    public int AgendasContaining(GoalKind kind) => _agendasContaining[(int)kind];

    /// <summary>
    /// Fold one agenda into the totals. Cheap — the agenda holds at most a few dozen goals and this runs
    /// once per prompt.
    /// </summary>
    public void Record(BotAgenda agenda)
    {
        if (agenda == null) return;

        AgendasBuilt++;

        if (agenda.Goals.Count == 0)
        {
            EmptyAgendas++;
            return;
        }

        _topped[(int)agenda.Goals[0].Kind]++;

        for (int i = 0; i < AllKinds.Length; i++)
        {
            int count = agenda.CountOf(AllKinds[i]);
            if (count == 0) continue;

            _proposed[i] += count;
            _agendasContaining[i]++;
        }
    }
}
