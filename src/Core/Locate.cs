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

    /// <summary>
    /// What a grab is short of that chests further away might hold: everything short, except
    /// what is short because the inventory has no room left, which more chests would not change.
    /// </summary>
    public static IReadOnlyList<string> SoughtFurther(Plan plan)
        => plan.Outcomes.Where(o => o.Short > 0 && !o.OutOfRoom).Select(o => o.Name).ToList();

    /// <summary>
    /// What a short grab says of the chests past the range it takes from that hold the item: a
    /// line per item any of them holds, nothing for the rest.
    /// </summary>
    /// <param name="parts">Each item short, with every chest looked in: its distance and how many of the item it holds.</param>
    /// <param name="range">How far the grab takes from; chests within it are the grab's own.</param>
    /// <param name="shown">The item's name as the player reads it.</param>
    public static IReadOnlyList<string> TooFar(IEnumerable<(string Name, IReadOnlyList<(float Distance, int Count)> Chests)> parts, float range, Func<string, string> shown)
    {
        var lines = new List<string>();
        foreach (var (name, chests) in parts)
        {
            var holding = chests.Where(c => c.Count > 0 && c.Distance > range).ToList();
            if (holding.Count == 0)
            {
                continue;
            }

            string where = holding.Count == 1 ? "1 chest" : $"{Number(holding.Count)} chests";
            lines.Add($"{shown(name)}: {Number(holding.Sum(c => c.Count))} in {where}, nearest {Number(holding.Min(c => c.Distance))} m, too far to take");
        }

        return lines;
    }

    /// <summary>A ping's name on the map: what the chest holds of each item looked for, in that order.</summary>
    public static string PinName(IEnumerable<(string Name, int Count)> held, Func<string, string> shown)
        => string.Join(" · ", held.Where(h => h.Count > 0).Select(h => $"{shown(h.Name)} {Number(h.Count)}"));

    private static string Number(float value) => Math.Round(value).ToString("N0", CultureInfo.InvariantCulture);
}
