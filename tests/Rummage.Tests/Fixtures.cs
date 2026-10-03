using System;
using System.Collections.Generic;
using System.Linq;
using Rummage.Core;

namespace Rummage.Tests;

/// <summary>Chests, inventories and items as the tests describe them.</summary>
internal static class Fixtures
{
    public const string Wood = "$item_wood";
    public const string Stone = "$item_stone";
    public const string Nails = "$item_bronzenails";
    public const string Sword = "$item_sword_iron";

    public static ItemStack Stack(string name, int count, int x = 0, int y = 0, int maxStack = 50,
        int quality = 1, int worldLevel = 0, bool qualityMatters = false)
        => new(name, PrefabOf(name), count, maxStack, x, y, quality, worldLevel, qualityMatters);

    public static string PrefabOf(string name) => name switch
    {
        Wood => "Wood",
        Stone => "Stone",
        Nails => "BronzeNails",
        Sword => "SwordIron",
        _ => name.Replace("$item_", "", StringComparison.Ordinal),
    };

    public static Source Chest(int id, float distance, params ItemStack[] stacks)
        => new(id, distance, Reach.Owned, stacks, mayTake: null);

    public static Source Chest(int id, float distance, Reach reach, params ItemStack[] stacks)
        => new(id, distance, reach, stacks, mayTake: null);

    public static Source Chest(int id, float distance, Func<string, bool> mayTake, params ItemStack[] stacks)
        => new(id, distance, Reach.Owned, stacks, mayTake);

    /// <summary>An empty inventory of the game's usual size, unless told otherwise.</summary>
    public static Bag Inventory(params ItemStack[] stacks) => new(8, 4, stacks, isUsable: null);

    public static Bag Inventory(int width, int height, params ItemStack[] stacks) => new(width, height, stacks, isUsable: null);

    public static Want Bring(string name, int count) => new(name, count, countCarried: false, minWorldLevel: 0);

    public static Want MakeSureICarry(string name, int count) => new(name, count, countCarried: true, minWorldLevel: 0);

    public static Plan Plan(Bag bag, bool leaveOne, Want[] wants, params Source[] chests)
        => Planner.Make(wants, chests, bag, leaveOne);

    public static int Planned(this Plan plan, string name) => plan.Moves.Where(m => m.Name == name).Sum(m => m.Amount);

    public static int TakenFrom(this Plan plan, int chest) => plan.Moves.Where(m => m.SourceId == chest).Sum(m => m.Amount);

    public static Outcome For(this Plan plan, string name) => plan.Outcomes.Single(o => o.Name == name);

    /// <summary>The inventory after every move of the plan landed, slot by slot.</summary>
    public static Dictionary<(int X, int Y), (string Name, int Count)> After(this Plan plan, Bag bag)
    {
        var slots = bag.Stacks.ToDictionary(s => (s.X, s.Y), s => (s.Name, s.Count));
        foreach (var move in plan.Moves)
        {
            var at = (move.ToX, move.ToY);
            slots[at] = slots.TryGetValue(at, out var held) ? (held.Name, held.Count + move.Amount) : (move.Name, move.Amount);
        }
        return slots;
    }
}
