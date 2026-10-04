using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Rummage.Core;

/// <summary>What finding an item says: how many there are, in how many chests, and how far the nearest is.</summary>
internal static class Locate
{
    /// <param name="parts">Each item looked for, with every chest looked in: its distance and how many of the item it holds.</param>
    /// <param name="radius">How far was looked, said when nothing was found.</param>
    /// <param name="shown">The item's name as the player reads it.</param>
    public static IReadOnlyList<string> Lines(IEnumerable<(string Name, IReadOnlyList<(float Distance, int Count)> Chests)> parts, float radius, Func<string, string> shown)
    {
        var lines = new List<string>();
        foreach (var (name, chests) in parts)
        {
            var holding = chests.Where(c => c.Count > 0).ToList();
            if (holding.Count == 0)
            {
                lines.Add($"{shown(name)}: in no chest within {Number(radius)} m");
                continue;
            }

            int total = holding.Sum(c => c.Count);
            float nearest = holding.Min(c => c.Distance);
            string where = holding.Count == 1 ? "1 chest" : $"{Number(holding.Count)} chests";
            lines.Add($"{shown(name)}: {Number(total)} in {where}, nearest {Number(nearest)} m");
        }

        return lines;
    }

    private static string Number(float value) => Math.Round(value).ToString("N0", CultureInfo.InvariantCulture);
}
