using System;
using System.Collections.Generic;
using System.Linq;
using Rummage.Core;

namespace Rummage.Game;

/// <summary>
/// What the search box offers: the items the chests in reach can give, the build pieces the
/// player knows and the recipes the player knows, each with what one of it is.
/// </summary>
internal sealed class Catalog
{
    private readonly Dictionary<Entry, Target> _targets = new();
    private readonly Dictionary<string, int> _stackSizes = new(StringComparer.Ordinal);

    private Catalog()
    {
    }

    public List<Entry> Entries { get; } = new();

    /// <summary>What the inventory holds that counts, by item name, as the search box opened.</summary>
    public Dictionary<string, int> Carried { get; private set; } = new();

    /// <summary>What the chests in reach can give, by item name, as the search box opened.</summary>
    public Dictionary<string, int> InChests { get; private set; } = new();

    public Target TargetOf(Entry entry) => _targets.TryGetValue(entry, out var target) ? target : null;

    public UnityEngine.Sprite IconOf(Entry entry) => TargetOf(entry)?.Icon;

    /// <summary>How many of an item make a full stack, for the count a grab starts at.</summary>
    public int StackSize(string name) => _stackSizes.TryGetValue(name, out int size) ? size : 1;

    public static Catalog Build(Player player, Scene scene)
    {
        var catalog = new Catalog();
        var stock = Stock.Takeable(scene.Sources, CraftyLink.LeaveOne, global::Game.m_worldLevel);
        var carried = Scene.Carried(player);
        catalog.Carried = carried;
        catalog.InChests = stock;

        foreach (var stack in scene.Sources.SelectMany(s => s.Stacks))
        {
            catalog._stackSizes[stack.Name] = stack.MaxStack;
        }

        var held = Held(player, scene);
        foreach (string name in held.Keys.Union(stock.Keys))
        {
            int takeable = stock.TryGetValue(name, out int t) ? t : 0;
            catalog.Add(new Entry(name, Text.Shown(name), EntryKind.Item, takeable, held.TryGetValue(name, out int h) ? h : 0),
                Target.ForItem(name, Text.Shown(name), 1));
        }

        foreach (var (key, unit) in Costed(player))
        {
            catalog.Guarded(key, () => catalog.AddCosted(unit, key, carried, stock));
        }

        return catalog;
    }

    /// <summary>
    /// Everything the chests hold that finding would look in (AzuCraftyBoxes' registry within the
    /// find radius), also what Leave One Item or chest rules keep back, so an item that cannot be
    /// taken can still be searched for and found.
    /// </summary>
    private static Dictionary<string, int> Held(Player player, Scene scene)
    {
        var held = new Dictionary<string, int>(StringComparer.Ordinal);
        void Count(Inventory inventory, Dictionary<string, int> into)
        {
            foreach (var item in inventory.GetAllItems())
            {
                if (item?.m_shared != null)
                {
                    into[item.m_shared.m_name] = (into.TryGetValue(item.m_shared.m_name, out int n) ? n : 0) + item.m_stack;
                }
            }
        }

        foreach (var chest in CraftyLink.Registered(player.transform.position, Finder.Radius))
        {
            if (chest && chest.GetInventory() != null && Access.MayOpen(chest, player) && (CraftyLink.OnlyChests == null || CraftyLink.OnlyChests(chest)))
            {
                Count(chest.GetInventory(), held);
            }
        }

        var reachable = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var chest in scene.Chests.Values)
        {
            if (chest && chest.GetInventory() != null && Access.MayOpen(chest, player))
            {
                Count(chest.GetInventory(), reachable);
            }
        }

        foreach (var item in reachable)
        {
            held[item.Key] = Math.Max(item.Value, held.TryGetValue(item.Key, out int n) ? n : 0);
        }

        return held;
    }

    private static readonly HarmonyLib.AccessTools.FieldRef<Player, HashSet<string>> KnownRecipes = HarmonyLib.AccessTools.FieldRefAccess<Player, HashSet<string>>("m_knownRecipes");
    private static readonly HarmonyLib.AccessTools.FieldRef<Player, HashSet<string>> KnownMaterials = HarmonyLib.AccessTools.FieldRefAccess<Player, HashSet<string>>("m_knownMaterial");
    private static readonly List<(string Key, Target Unit)> CostedCache = new();
    private static (int Db, long Player, int Recipes, int Materials, string Language) _costedFor;

    /// <summary>
    /// Every piece the build menus would show and every recipe the player knows, with what one
    /// costs and its icon. These change only when the player learns something, so they are kept
    /// between openings and made again only when the known recipes or materials, the player, the
    /// world's item list or the language changed.
    /// </summary>
    private static List<(string Key, Target Unit)> Costed(Player player)
    {
        var key = (ObjectDB.instance ? ObjectDB.instance.GetInstanceID() : 0, player.GetPlayerID(),
            KnownRecipes(player)?.Count ?? -1, KnownMaterials(player)?.Count ?? -1, Localization.instance?.GetSelectedLanguage() ?? string.Empty);
        if (key == _costedFor && CostedCache.Count > 0)
        {
            return CostedCache;
        }

        _costedFor = key;
        CostedCache.Clear();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var piece in KnownPieces(player))
        {
            if (seen.Add(piece.m_name))
            {
                Read(piece.m_name, () => CostedCache.Add((piece.m_name, Target.ForPiece(piece))));
            }
        }

        seen.Clear();
        foreach (var recipe in ObjectDB.instance ? ObjectDB.instance.m_recipes : [])
        {
            if (recipe && recipe.m_enabled && recipe.m_item && player.IsRecipeKnown(recipe.m_item.m_itemData.m_shared.m_name)
                && seen.Add(recipe.m_item.m_itemData.m_shared.m_name))
            {
                Read(recipe.name, () => CostedCache.Add((recipe.m_item.m_itemData.m_shared.m_name, Target.ForRecipe(recipe, 1, null, player, 1))));
            }
        }

        return CostedCache;
    }

    /// <summary>Forgets the kept pieces and recipes, so the next opening makes them again, as the self-test measures it.</summary>
    internal static void Forget() => CostedCache.Clear();

    private static void Read(string what, Action add)
    {
        try
        {
            add();
        }
        catch (Exception e)
        {
            if (Unreadable.Add(what))
            {
                Plugin.Log.LogWarning($"Left {what} out of the search box: {e.GetType().Name}: {e.Message}");
            }
        }
    }

    /// <summary>One piece or recipe that cannot be read is left out of the list, told once, rather than keeping the search box shut.</summary>
    private void Guarded(string what, Action add)
    {
        try
        {
            add();
        }
        catch (Exception e)
        {
            if (Unreadable.Add(what))
            {
                Plugin.Log.LogWarning($"Left {what} out of the search box: {e.GetType().Name}: {e.Message}");
            }
        }
    }

    private static readonly HashSet<string> Unreadable = new(StringComparer.Ordinal);

    private void Add(Entry entry, Target target)
    {
        Entries.Add(entry);
        _targets[entry] = target;
    }

    /// <summary>A piece or recipe, listed with how many of it what is carried and what the chests give would pay for.</summary>
    private void AddCosted(Target target, string key, Dictionary<string, int> carried, Dictionary<string, int> stock)
    {
        if (target.Cost.Count == 0)
        {
            return;
        }

        int affordable = target.AnyOf
            ? target.Cost.Max(part => Stock.Affordable(new Dictionary<string, int> { [part.Name] = part.Amount }, carried, stock))
            : Stock.Affordable(Cost.Sum(target.Cost), carried, stock);
        Add(new Entry(key, target.Shown, target.Kind, affordable), target);
    }

    /// <summary>The pieces the build menu of any build tool would show the player.</summary>
    private static IEnumerable<Piece> KnownPieces(Player player)
    {
        if (!ObjectDB.instance)
        {
            yield break;
        }

        foreach (var prefab in ObjectDB.instance.m_items)
        {
            var table = prefab && prefab.TryGetComponent(out ItemDrop drop) ? drop.m_itemData?.m_shared?.m_buildPieces : null;
            if (!table)
            {
                continue;
            }

            foreach (var piecePrefab in table.m_pieces)
            {
                if (piecePrefab && piecePrefab.TryGetComponent(out Piece piece) && piece.m_enabled && player.IsRecipeKnown(piece.m_name))
                {
                    yield return piece;
                }
            }
        }
    }
}
