using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

/// <summary>
/// Resolves a type by the bare name data files use for it — a card's <c>ExecutionClass</c>, for one.
///
/// <see cref="Type.GetType(string)"/> only searches the calling assembly and the core library, so a card
/// class that lives in an expansion assembly would never be found by it. Core types are tried that way
/// first, exactly as before, so every base card resolves to the same type it always did; only a name core
/// does not know falls through to the loaded expansion assemblies.
/// </summary>
public static class TypeRegistry
{
    private static readonly List<Assembly> _expansionAssemblies = new();

    // Expansion lookups only. Misses are cached too (as null): a typo in a data file would otherwise scan
    // every expansion assembly on each lookup.
    private static readonly ConcurrentDictionary<string, Type> _expansionCache = new();

    /// <summary>Called by <see cref="ExpansionLoader"/> for each expansion assembly, before any data loads.</summary>
    internal static void AddAssembly(Assembly assembly)
    {
        _expansionAssemblies.Add(assembly);
        _expansionCache.Clear();
    }

    /// <summary>The type called <paramref name="name"/>, or null if neither core nor an expansion has one.</summary>
    public static Type Resolve(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;

        Type coreType = Type.GetType(name);
        if (coreType != null) return coreType;

        return _expansionCache.GetOrAdd(name, FindInExpansions);
    }

    private static Type FindInExpansions(string name)
    {
        List<Type> matches = _expansionAssemblies
            .Select(assembly => assembly.GetType(name))
            .Where(type => type != null)
            .ToList();

        if (matches.Count > 1)
            throw new InvalidOperationException(
                $"Type '{name}' is defined by more than one expansion: {string.Join(", ", matches.Select(t => t.Assembly.GetName().Name))}");

        return matches.FirstOrDefault();
    }
}
