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

        foreach (var item in stock)
        {
            catalog.Add(new Entry(item.Key, Text.Shown(item.Key), EntryKind.Item, item.Value), Target.ForItem(item.Key, Text.Shown(item.Key), 1));
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var piece in KnownPieces(player))
        {
            if (seen.Add(piece.m_name))
            {
                catalog.Guarded(piece.m_name, () => catalog.AddCosted(Target.ForPiece(piece), piece.m_name, carried, stock));
            }
        }

        seen.Clear();
        foreach (var recipe in ObjectDB.instance ? ObjectDB.instance.m_recipes : [])
        {
            if (recipe && recipe.m_enabled && recipe.m_item && player.IsRecipeKnown(recipe.m_item.m_itemData.m_shared.m_name)
                && seen.Add(recipe.m_item.m_itemData.m_shared.m_name))
            {
                catalog.Guarded(recipe.name, () => catalog.AddCosted(Target.ForRecipe(recipe, 1, null, player, 1), recipe.m_item.m_itemData.m_shared.m_name, carried, stock));
            }
        }

        return catalog;
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
