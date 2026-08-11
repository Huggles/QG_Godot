using Godot;
using System;
using System.Linq;

/// <summary>
/// The seed box shared by the two screens that can start a game — the single-player scenario select
/// and the multiplayer lobby. Both want the same three things (show the seed that will actually be
/// used, let it be typed over, re-roll it on demand), so the behaviour lives here instead of twice.
///
/// The field is pre-filled rather than left blank: the seed is only useful if a player can read back
/// the one that produced an interesting game, and an empty box would hide it until after the fact.
///
/// Not <see cref="GameRandom"/> for the roll — that is the game's own stream, re-seeded from whatever
/// this control produces. Seeding it from itself would be circular.
/// </summary>
public static class MenuSeedField
{
    private static readonly Random SeedSource = new();

    /// <summary>Upper bound of a rolled seed. Nine digits keeps any typed value inside int range,
    /// which is what lets <see cref="Read"/> parse without an overflow branch.</summary>
    private const int MAX_SEED = 999_999_999;

    /// <summary>Wire up a seed row: pre-fill with a fresh roll, restrict typing to digits, and hook
    /// the randomize button. Either control may be null, so a screen can offer just the field.</summary>
    public static void Bind(LineEdit field, Button randomizeButton)
    {
        if (field == null) return;

        field.Text = Roll().ToString();
        field.TextChanged += text => KeepDigitsOnly(field, text);

        if (randomizeButton != null)
            randomizeButton.Pressed += () => field.Text = Roll().ToString();
    }

    /// <summary>
    /// The seed the field is asking for, rolling a fresh one if it was cleared. Writes the result
    /// back so the box always shows the seed the game actually starts with.
    /// </summary>
    public static int Read(LineEdit field)
    {
        if (field != null && int.TryParse(field.Text, out int typed) && typed > 0)
            return typed;

        int rolled = Roll();
        if (field != null) field.Text = rolled.ToString();
        return rolled;
    }

    /// <summary>Hand the field's seed to the session about to start. See GameManager.PendingSeed.</summary>
    public static void Commit(LineEdit field)
    {
        GameManager.PendingSeed = Read(field);
        DebugUtilities.PrintPeer($"Seed for next game: {GameManager.PendingSeed}");
    }

    public static int Roll() => SeedSource.Next(1, MAX_SEED);

    /// <summary>
    /// A seed is an int, so anything else typed is a value the game cannot honour. Filtering as it is
    /// typed rather than rejecting on start keeps the box showing exactly what will be used.
    /// </summary>
    private static void KeepDigitsOnly(LineEdit field, string text)
    {
        string digits = new string(text.Where(char.IsDigit).ToArray());
        // Nine digits max: longer values overflow int, and a silently truncated seed is worse than
        // one that cannot be typed in the first place.
        if (digits.Length > 9) digits = digits.Substring(0, 9);
        if (digits == text) return;

        int caret = field.CaretColumn;
        field.Text = digits;
        field.CaretColumn = Math.Min(caret, digits.Length);
    }
}
