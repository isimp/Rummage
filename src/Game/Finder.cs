using System.Collections.Generic;
using System.Linq;
using Rummage.Core;
using UnityEngine;

namespace Rummage.Game;

/// <summary>
/// Finds where something is without taking it: the chests within the find radius holding an item,
/// or for a piece or recipe any of its parts, light up for <c>FindGlow</c> seconds, get a ping on
/// the map, and a line per item says how many there are, in how many chests and how far the
/// nearest is. It looks in the chests AzuCraftyBoxes has registered, the ones the player may use
/// and ones someone else has open, and never in another player's private chest. A grab short of
/// something looks the same way at the chests past the range it takes from.
/// </summary>
internal static class Finder
{
    /// <summary>How far finding, and the search list, look for chests.</summary>
    public static float Radius => Settings.FindRadius.Value;

    /// <summary>What the last find said, for the self-test.</summary>
    internal static IReadOnlyList<string> LastLines { get; private set; } = [];

    /// <summary>The chests the last find lit, for the self-test.</summary>
    internal static IReadOnlyList<Container> LastLit { get; private set; } = [];

    /// <summary>The chests too far to take from that the last short grab lit, for the self-test.</summary>
    internal static IReadOnlyList<Container> LastFarLit { get; private set; } = [];

    public static void Find(Player player, Target target)
    {
        float radius = CraftyLink.HasRegistry ? Radius : CraftyLink.Range;
        var names = target.Kind == EntryKind.Item ? [target.Cost[0].Name] : Cost.Sum(target.Cost).Keys.ToList();
        var looked = Look(player, Chests(player), names);

        LastLit = Show(looked);
        if (LastLit.Count > 0)
        {
            Sound.Play(Sound.Cue.Find);
        }

        LastLines = Locate.Lines(looked.Parts, radius, Text.Shown);
        Notice.Say(player, LastLines);
    }

    /// <summary>
    /// For a grab short of the items named: the lines on the chests past the range it takes from
    /// that hold any of them, which are lit and pinged as a find lights them. Without
    /// AzuCraftyBoxes' registry there is nothing past that range to look in.
    /// </summary>
    public static IReadOnlyList<string> FindFar(Player player, Scene scene, IReadOnlyList<string> names)
    {
        LastFarLit = [];
        if (names.Count == 0 || !CraftyLink.HasRegistry)
        {
            return [];
        }

        float range = CraftyLink.Range;
        var taking = new HashSet<Container>(scene.Chests.Values);
        var here = player.transform.position;
        var chests = Chests(player).Where(c => !taking.Contains(c) && Vector3.Distance(here, c.transform.position) > range).ToList();
        var looked = Look(player, chests, names);

        LastFarLit = Show(looked);
        return Locate.TooFar(looked.Parts, range, Text.Shown);
    }

    /// <summary>The chests within the find radius the player may open.</summary>
    private static List<Container> Chests(Player player)
    {
        var chests = CraftyLink.HasRegistry
            ? CraftyLink.Registered(player.transform.position, Radius).ToList()
            : CraftyLink.NearbyChests(player);
        return chests.Where(c => c && c.GetInventory() != null && Access.MayOpen(c, player) && (CraftyLink.OnlyChests == null || CraftyLink.OnlyChests(c))).Distinct().ToList();
    }

    /// <summary>How many of each item every chest holds, and how far it is.</summary>
    private static Looked Look(Player player, List<Container> chests, IReadOnlyList<string> names)
    {
        var here = player.transform.position;
        var held = chests.ToDictionary(c => c, c => names.Select(name => (name, c.GetInventory().GetAllItems().Where(i => i.m_shared?.m_name == name).Sum(i => i.m_stack))).ToList());
        var parts = names.Select((name, n) => (name, (IReadOnlyList<(float, int)>)chests.Select(c => (Vector3.Distance(here, c.transform.position), held[c][n].Item2)).ToList())).ToList();
        return new Looked(parts, held);
    }

    /// <summary>Lights and pings every chest holding any of what was looked for, and gives them back.</summary>
    private static List<Container> Show(Looked looked)
    {
        var lit = looked.Held.Where(c => c.Value.Any(h => h.Count > 0)).Select(c => c.Key).ToList();
        foreach (var chest in lit)
        {
            Highlighter.Light(chest, Settings.FindGlow.Value);
        }

        Pinger.Ping(lit.Select(c => (c, Locate.PinName(looked.Held[c], Text.Shown))));
        return lit;
    }

    private sealed class Looked
    {
        public Looked(List<(string, IReadOnlyList<(float, int)>)> parts, Dictionary<Container, List<(string Name, int Count)>> held)
        {
            Parts = parts;
            Held = held;
        }

        public List<(string, IReadOnlyList<(float, int)>)> Parts { get; }

        public Dictionary<Container, List<(string Name, int Count)>> Held { get; }
    }
}
