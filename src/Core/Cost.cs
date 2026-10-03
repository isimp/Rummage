using System;
using System.Collections.Generic;

namespace Rummage.Core;

/// <summary>What one of something costs, by item name.</summary>
internal static class Cost
{
    /// <summary>
    /// The parts of a cost added up by item: a recipe or piece may list one ingredient more than
    /// once, and the game takes each listing.
    /// </summary>
    public static Dictionary<string, int> Sum(IEnumerable<(string Name, int Amount)> parts)
    {
        var cost = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (name, amount) in parts)
        {
            if (amount > 0)
            {
                cost[name] = (cost.TryGetValue(name, out int sum) ? sum : 0) + amount;
            }
        }

        return cost;
    }
}
