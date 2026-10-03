using System;
using System.Collections.Generic;
using System.Linq;

namespace Rummage.Core;

/// <summary>How many a press of the key is for: one more than what is carried already pays for.</summary>
internal static class Repeat
{
    /// <param name="cost">What one costs, by item name.</param>
    /// <param name="carried">What the inventory holds that counts, by item name.</param>
    /// <param name="step">How many one press adds: one piece, or the crafting tab's craft amount.</param>
    /// <param name="anyOf">A recipe that takes any one of its ingredients, so the best covered one counts.</param>
    public static int Next(IReadOnlyDictionary<string, int> cost, IReadOnlyDictionary<string, int> carried, int step, bool anyOf)
    {
        step = Math.Max(1, step);
        var times = cost.Where(p => p.Value > 0)
            .Select(p => (carried.TryGetValue(p.Key, out int have) ? have : 0) / p.Value)
            .ToList();
        if (times.Count == 0)
        {
            return step;
        }

        int paid = anyOf ? times.Max() : times.Min();
        return (paid / step + 1) * step;
    }
}
