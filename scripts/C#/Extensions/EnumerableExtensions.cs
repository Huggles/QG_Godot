using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public static class EnumerableExtensions
{
    
    /// <summary>
    /// Returns a NEW shuffled list — it does NOT shuffle in place. `list.Shuffle();` with the result
    /// discarded is a silent no-op, which is exactly the bug DeckState.ShuffleDeck used to have.
    /// To shuffle a list you already hold, use <c>GameRandom.Shuffle(list)</c>.
    /// </summary>
    public static IList<T> Shuffle<T>(this IEnumerable<T> sequence)
    {
        // Seeded, so a run is reproducible from GameRandom.Seed.
        return sequence.Shuffle(GameRandom.Raw);
    }

    public static IList<T> Shuffle<T>(this IEnumerable<T> sequence, Random randomNumberGenerator)
    {
        if (sequence == null)
        {
            throw new ArgumentNullException("sequence");
        }

        if (randomNumberGenerator == null)
        {
            throw new ArgumentNullException("randomNumberGenerator");
        }

        T swapTemp;
        List<T> values = sequence.ToList();
        int currentlySelecting = values.Count;
        while (currentlySelecting > 1)
        {
            int selectedElement = randomNumberGenerator.Next(currentlySelecting);
            --currentlySelecting;
            if (currentlySelecting != selectedElement)
            {
                swapTemp = values[currentlySelecting];
                values[currentlySelecting] = values[selectedElement];
                values[selectedElement] = swapTemp;
            }
        }

        return values;
    }
}
