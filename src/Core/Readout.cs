using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Rummage.Core;

/// <summary>What you carry for something, shown before a press: while a piece is selected, and in the search box.</summary>
internal static class Readout
{
    /// <summary>Valheim's orange, for what the key would bring.</summary>
    public const string Coming = "#FFA13C";

    /// <summary>For what cannot be had: not carried and not in the chests.</summary>
    public const string Missing = "#E8604C";

    /// <summary>For the header row of the build panel's table.</summary>
    public const string Header = "#A89F91";

    /// <summary>The space after each column of the build panel's table, in text units.</summary>
    private const float Gap = 24f;

    /// <summary>
    /// The table beside the game's build panel: a header naming the count and the key, then one
    /// row per part with what the inventory holds against what the next press is for (orange
    /// where the press would make it up, red where it cannot) and what the press would take.
    /// Columns start after the widest entry before them, measured by <paramref name="width"/> in
    /// the text's own units.
    /// </summary>
    public static string Panel(Plan plan, int count, string key, Func<string, string> shown, Func<string, float> width)
    {
        var rows = plan.Outcomes.Select(o => (Name: shown(o.Name), Carried: $"{Number(o.Carried)} / {Number(o.Wanted)}", Outcome: o)).ToList();
        string head = $"For {Number(count)}";
        float carriedAt = rows.Select(r => width(r.Name)).Append(width(head)).Max() + Gap;
        float takesAt = carriedAt + rows.Select(r => width(r.Carried)).Append(width("carried")).Max() + Gap;
        string carriedPos = $"<pos={Position(carriedAt)}>";
        string takesPos = $"<pos={Position(takesAt)}>";

        var lines = new List<string> { $"<color={Header}>{head}{carriedPos}carried{takesPos}{key} takes</color>" };
        foreach (var (name, carried, o) in rows)
        {
            string have = o.Need == 0 ? carried : Paint(o.Short > 0 ? Missing : Coming, carried);
            lines.Add($"{name}{carriedPos}{have}{takesPos}{Takes(o)}");
        }

        if (plan.Outcomes.All(o => o.Need == 0))
        {
            lines.Add("You carry it all");
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// The table beside the build panel when every press brings a piece's whole cost
    /// (<see cref="PiecePress.FullSet"/>): a header naming the key, then one row per part with
    /// what the inventory holds, as a plain number since it does not count towards the press, and
    /// what the press takes, marked as in <see cref="Panel"/> where the chests cannot give it all.
    /// </summary>
    public static string FullSetPanel(Plan plan, IReadOnlyDictionary<string, int> carried, string key, Func<string, string> shown, Func<string, float> width)
    {
        const string Head = "One more";
        var rows = plan.Outcomes.Select(o => (Name: shown(o.Name), Carried: Number(carried.TryGetValue(o.Name, out int c) ? c : 0), Outcome: o)).ToList();
        float carriedAt = rows.Select(r => width(r.Name)).Append(width(Head)).Max() + Gap;
        float takesAt = carriedAt + rows.Select(r => width(r.Carried)).Append(width("carried")).Max() + Gap;
        string carriedPos = $"<pos={Position(carriedAt)}>";
        string takesPos = $"<pos={Position(takesAt)}>";

        var lines = new List<string> { $"<color={Header}>{Head}{carriedPos}carried{takesPos}{key} takes</color>" };
        lines.AddRange(rows.Select(r => $"{r.Name}{carriedPos}{r.Carried}{takesPos}{Takes(r.Outcome)}"));
        return string.Join("\n", lines);
    }

    /// <summary>
    /// Whether the build box shows, as set, given the plan for one of the piece: with
    /// <see cref="BuildBox.UntilCarried"/> it hides while the inventory holds enough to build one.
    /// </summary>
    public static bool Shows(Plan plan, BuildBox setting) => setting switch
    {
        BuildBox.Always => true,
        BuildBox.UntilCarried => plan.Outcomes.Any(o => o.Need > 0),
        _ => false,
    };

    /// <summary>A part's last column: how many the press takes, and how many would still be short.</summary>
    private static string Takes(Outcome o)
    {
        if (o.Need == 0)
        {
            return "";
        }

        if (o.Planned == 0)
        {
            return Paint(Missing, "none");
        }

        string takes = Paint(Coming, Number(o.Planned));
        return o.Short > 0 ? $"{takes} · {Paint(Missing, $"{Number(o.Short)} short")}" : takes;
    }

    private static string Paint(string color, string text) => $"<color={color}>{text}</color>";

    private static string Position(float at) => at.ToString("0", CultureInfo.InvariantCulture);

    /// <summary>
    /// A piece's or recipe's parts in the search box: carried, plus what the chests give, against
    /// what the count needs. When what is carried does not count (<paramref name="countsCarried"/>
    /// false, a piece under <see cref="PiecePress.FullSet"/>), what the chests give against what
    /// the count brings.
    /// </summary>
    public static string Parts(IReadOnlyDictionary<string, int> cost, int count, IReadOnlyDictionary<string, int> carried,
        IReadOnlyDictionary<string, int> takeable, Func<string, string> shown, bool countsCarried = true)
        => string.Join(" · ", cost.Where(p => p.Value > 0).Select(part =>
        {
            int need = part.Value * Math.Max(1, count);
            int have = countsCarried && carried.TryGetValue(part.Key, out int c) ? c : 0;
            int give = takeable.TryGetValue(part.Key, out int t) ? t : 0;
            string text = countsCarried
                ? $"{shown(part.Key)} {Number(have)} + {Number(give)} / {Number(need)}"
                : $"{shown(part.Key)} {Number(give)} / {Number(need)}";
            return have + give < need ? $"{text} (short)" : text;
        }));

    /// <summary>
    /// An item's row in the search box: whether there is anything to take of what the chests hold
    /// nearby, or that none is nearby, for an item listed because the search box last used it.
    /// </summary>
    public static string Stocked(int takeable, int held)
    {
        if (takeable == 0 && held == 0)
        {
            return "none nearby";
        }

        if (takeable >= held)
        {
            return $"{Number(takeable)} in chests";
        }

        return takeable > 0 ? $"{Number(takeable)} to take, {Number(held)} nearby" : $"{Number(held)} nearby, none to take";
    }

    /// <summary>The new items row in the search box: how many could be taken of those nearby.</summary>
    public static string NewItems(int takeable, int seen)
    {
        if (takeable >= seen)
        {
            return $"{Number(takeable)} new to you";
        }

        return takeable > 0 ? $"{Number(takeable)} to take, {Number(seen)} new nearby" : $"{Number(seen)} new nearby, none to take";
    }

    /// <summary>The new items row when chosen: the first few names it would take.</summary>
    public static string NewNames(IReadOnlyList<string> shown)
    {
        const int Named = 4;
        string names = string.Join(", ", shown.Take(Named));
        return shown.Count > Named ? $"One each of {names} and {Number(shown.Count - Named)} more" : $"One each of {names}";
    }

    /// <summary>An item in the search box: carried, and what the chests give.</summary>
    public static string Item(int carried, int inChests) => $"{Number(carried)} carried + {Number(inChests)} in chests";

    private static string Number(int count) => count.ToString("N0", CultureInfo.InvariantCulture);
}
