using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Rummage.Core;

/// <summary>What you carry for something, shown before a press: while a piece is selected, and in the search box.</summary>
internal static class Readout
{
    /// <summary>
    /// The line shown while a piece is selected: what the inventory holds of each part against
    /// what the next press is for, what that press would bring, and what would still be short.
    /// </summary>
    public static string Line(string label, Plan plan, string key, Func<string, string> shown)
    {
        string bag = string.Join(" · ", plan.Outcomes.Select(o => $"{shown(o.Name)} {Number(o.Carried)}/{Number(o.Wanted)}"));
        if (plan.Outcomes.All(o => o.Need == 0))
        {
            return $"{label} · in your bag: {bag} · you carry it all";
        }

        var brings = plan.Outcomes.Where(o => o.Planned > 0).Select(o => $"{Number(o.Planned)} {shown(o.Name)}").ToList();
        var shorts = plan.Outcomes.Where(o => o.Short > 0).Select(o => shown(o.Name)).ToList();
        string line = $"{label} · in your bag: {bag} · {key} brings {(brings.Count > 0 ? string.Join(", ", brings) : "nothing")}";
        return shorts.Count > 0 ? $"{line} · short: {string.Join(", ", shorts)}" : line;
    }

    /// <summary>A piece's or recipe's parts in the search box: carried, plus what the chests give, against what the count needs.</summary>
    public static string Parts(IReadOnlyDictionary<string, int> cost, int count, IReadOnlyDictionary<string, int> carried,
        IReadOnlyDictionary<string, int> takeable, Func<string, string> shown)
        => string.Join(" · ", cost.Where(p => p.Value > 0).Select(part =>
        {
            int need = part.Value * Math.Max(1, count);
            int have = carried.TryGetValue(part.Key, out int c) ? c : 0;
            int give = takeable.TryGetValue(part.Key, out int t) ? t : 0;
            string text = $"{shown(part.Key)} {Number(have)} + {Number(give)} / {Number(need)}";
            return have + give < need ? $"{text} (short)" : text;
        }));

    /// <summary>An item in the search box: carried, and what the chests give.</summary>
    public static string Item(int carried, int inChests) => $"{Number(carried)} carried + {Number(inChests)} in chests";

    private static string Number(int count) => count.ToString("N0", CultureInfo.InvariantCulture);
}
