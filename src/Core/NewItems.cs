using System;
using System.Collections.Generic;
using System.Linq;

namespace Rummage.Core;

/// <summary>
/// One of every item the chests nearby hold that the player has never carried. Carrying an item
/// once teaches it to the game, and with it the recipes and pieces that use it.
/// </summary>
internal static class NewItems
{
    /// <summary>The search box row's key; no item has it as a name.</summary>
    public const string Key = "rummage:new";

    public const string Label = "New items";

    /// <summary>What is told when the chests nearby hold nothing new.</summary>
    public const string NothingNew = "Nothing in the chests nearby is new to you";

    /// <summary>
    /// One of each item new to the player, nearest chest first. Chests the player may not use are
    /// not looked into, so a private chest gives nothing away. Any world level teaches the item.
    /// </summary>
    public static List<Want> Wants(IEnumerable<Source> sources, Func<string, bool> known)
        => Unknown(sources, known).Select(Want).ToList();

    /// <summary>A want for one of the item, from any world level.</summary>
    public static Want Want(string name) => new(name, 1, countCarried: false, minWorldLevel: 0);

    /// <summary>For the search box row: how many new items a grab could take, and how many the chests nearby hold.</summary>
    public static (int Takeable, int Seen) Count(IEnumerable<Source> sources, Func<string, bool> known, bool leaveOne)
    {
        var chests = sources.ToList();
        var unknown = Unknown(chests, known);
        var stock = Stock.Takeable(chests, leaveOne, minWorldLevel: 0);
        return (unknown.Count(name => stock.ContainsKey(name)), unknown.Count);
    }

    private static List<string> Unknown(IEnumerable<Source> sources, Func<string, bool> known)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return sources.Where(s => s.Reach != Reach.Barred)
            .OrderBy(s => s.Distance)
            .ThenBy(s => s.Id)
            .SelectMany(s => s.Stacks)
            .Select(s => s.Name)
            .Where(name => !known(name) && seen.Add(name))
            .ToList();
    }
}
