using System;
using System.Collections.Generic;
using System.Linq;

namespace Rummage.Core;

/// <summary>What the search box lists, with no game in it; the game side reads the chests and the player's recipes.</summary>
internal static class CatalogRules
{
    /// <summary>
    /// The items listed: what the chests in reach can give, what the chests within the find radius
    /// hold, so an item kept back can still be found, and what the box last found or took, even once
    /// no chest holds it.
    /// </summary>
    public static List<string> ItemNames(IEnumerable<string> held, IEnumerable<string> takeable, IEnumerable<string> lastUsed) =>
        held.Union(takeable, StringComparer.Ordinal).Union(lastUsed, StringComparer.Ordinal).ToList();

    /// <summary>What a chest in reach holds counts in full, even where the find radius saw less of it.</summary>
    public static void MergeReachable(Dictionary<string, int> held, Dictionary<string, int> reachable)
    {
        foreach (var item in reachable)
        {
            held[item.Key] = Math.Max(item.Value, held.TryGetValue(item.Key, out int n) ? n : 0);
        }
    }

    /// <summary>Whether a piece or recipe is listed, as the ShowPieces and ShowRecipes settings say.</summary>
    public static bool Lists(EntryKind kind, bool pieces, bool recipes) => kind == EntryKind.Piece ? pieces : recipes;
}
