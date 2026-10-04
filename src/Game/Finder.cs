using System.Collections.Generic;
using System.Linq;
using Rummage.Core;
using UnityEngine;

namespace Rummage.Game;

/// <summary>
/// Finds where something is without taking it: the chests within 50 m holding an item, or for a
/// piece or recipe any of its parts, light up for a few seconds and a line per item says how many
/// there are, in how many chests and how far the nearest is. It looks in the chests AzuCraftyBoxes
/// has registered, the ones the player may use and ones someone else has open, and never in
/// another player's private chest.
/// </summary>
internal static class Finder
{
    public const float Radius = 50f;

    private const float LitFor = 6f;

    /// <summary>What the last find said, for the self-test.</summary>
    internal static IReadOnlyList<string> LastLines { get; private set; } = [];

    /// <summary>The chests the last find lit, for the self-test.</summary>
    internal static IReadOnlyList<Container> LastLit { get; private set; } = [];

    public static void Find(Player player, Target target)
    {
        var here = player.transform.position;
        float radius = Radius;
        var chests = CraftyLink.HasRegistry
            ? CraftyLink.Registered(here, radius).ToList()
            : CraftyLink.NearbyChests(player);
        if (!CraftyLink.HasRegistry)
        {
            radius = CraftyLink.Range;
        }

        chests = chests.Where(c => c && c.GetInventory() != null && (CraftyLink.OnlyChests == null || CraftyLink.OnlyChests(c))).ToList();
        var names = target.Kind == EntryKind.Item ? [target.Cost[0].Name] : Cost.Sum(target.Cost).Keys.ToList();
        var lit = new List<Container>();
        var parts = new List<(string, IReadOnlyList<(float, int)>)>();
        foreach (string name in names)
        {
            var found = new List<(float, int)>();
            foreach (var chest in chests)
            {
                int count = chest.GetInventory().GetAllItems().Where(i => i.m_shared?.m_name == name).Sum(i => i.m_stack);
                found.Add((Vector3.Distance(here, chest.transform.position), count));
                if (count > 0 && !lit.Contains(chest))
                {
                    lit.Add(chest);
                }
            }

            parts.Add((name, found));
        }

        foreach (var chest in lit)
        {
            Highlighter.Light(chest, LitFor);
        }

        LastLit = lit;
        LastLines = Locate.Lines(parts, radius, Text.Shown);
        foreach (string line in LastLines)
        {
            player.Message(MessageHud.MessageType.TopLeft, line);
        }
    }
}
