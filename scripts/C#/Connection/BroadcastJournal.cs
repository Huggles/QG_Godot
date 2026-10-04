using System.Collections.Generic;

/// <summary>
/// Host-only: every ChangeEvent as it went on the wire, in wire order. What a rejoining player is sent
/// to catch up.
///
/// Not GameMessages: an event is recorded there only after its animations finish, so a log read from it
/// can miss an event every other client already has. A restored game is seeded with the save's log,
/// since a replay broadcasts nothing.
/// </summary>
public static class BroadcastJournal
{
    private static readonly List<GameMessageDto> _events = new();

    public static void Record(GameMessageDto dto) => _events.Add(dto);

    public static void Seed(IEnumerable<GameMessageDto> events)
    {
        _events.Clear();
        _events.AddRange(events);
    }

    public static List<GameMessageDto> Snapshot() => new(_events);

    public static void Clear() => _events.Clear();
}
