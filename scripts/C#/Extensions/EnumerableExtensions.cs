using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public static class EnumerableExtensions
{
    
    /// <summary>
    /// Note this returns a NEW list; it does not shuffle in place. `list.Shuffle();` with the result
    /// discarded is a no-op — see DeckState.ShuffleDeck.
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
