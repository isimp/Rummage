using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Rummage.Core;

namespace Rummage.Game;

/// <summary>What a grab is for: what one of it costs, and how many.</summary>
internal sealed class Target
{
    public Target(string shown, EntryKind kind, IReadOnlyList<(string Name, int Amount)> cost, int count, bool anyOf)
    {
        Shown = shown;
        Kind = kind;
        Cost = cost;
        Count = count < 1 ? 1 : count;
        AnyOf = anyOf;
    }

    public string Shown { get; }

    public EntryKind Kind { get; }

    /// <summary>What one costs; for an item, one of itself.</summary>
    public IReadOnlyList<(string Name, int Amount)> Cost { get; }

    public int Count { get; }

    /// <summary>A recipe that takes any one of its ingredients rather than all of them.</summary>
    public bool AnyOf { get; }

    public string Label => Count > 1 ? $"{Shown} x{Count}" : Shown;

    public Target WithCount(int count) => new(Shown, Kind, Cost, count, AnyOf);

    /// <summary>
    /// What to ask the chests for. A piece or recipe is a total to carry, so what the inventory
    /// already holds counts; an item is a number to bring.
    /// </summary>
    public List<Want> Wants() => Cost.Select(Want).ToList();

    public Want Want((string Name, int Amount) part)
        => new(part.Name, part.Amount * Count, countCarried: Kind != EntryKind.Item, minWorldLevel: global::Game.m_worldLevel);

    public static Target ForItem(string name, string shown, int count)
        => new(shown, EntryKind.Item, [(name, 1)], count, anyOf: false);

    /// <summary>What one of the piece costs.</summary>
    public static Target ForPiece(Piece piece, int count = 1)
    {
        var cost = new List<(string, int)>();
        foreach (var requirement in piece.m_resources ?? [])
        {
            var item = requirement?.m_resItem ? requirement.m_resItem.m_itemData : null;
            if (item?.m_shared != null && requirement.m_amount > 0)
            {
                cost.Add((item.m_shared.m_name, requirement.m_amount));
            }
        }

        return new Target(Text.Shown(piece.m_name), EntryKind.Piece, cost, count, anyOf: false);
    }

    /// <summary>
    /// What one craft of the recipe costs at a quality level, by the game's rules: the amount for
    /// that level, an upgrade station's own parts only at such a station, and for a recipe that
    /// takes any one ingredient only those the player knows.
    /// </summary>
    public static Target ForRecipe(Recipe recipe, int quality, CraftingStation station, Player player, int count)
    {
        var cost = new List<(string, int)>();
        foreach (var requirement in recipe.m_resources ?? [])
        {
            var item = requirement?.m_resItem ? requirement.m_resItem.m_itemData : null;
            if (item?.m_shared == null || requirement.GetAmount(quality) <= 0)
            {
                continue;
            }

            bool stationFits = station ? station.m_upgrader == requirement.m_upgraderResource : !requirement.m_upgraderResource;
            if (stationFits && (!recipe.m_requireOnlyOneIngredient || player.IsKnownMaterial(item.m_shared.m_name)))
            {
                cost.Add((item.m_shared.m_name, requirement.GetAmount(quality)));
            }
        }

        return new Target(Text.Shown(recipe.m_item.m_itemData.m_shared.m_name), EntryKind.Recipe, cost, count, recipe.m_requireOnlyOneIngredient);
    }
}

/// <summary>Works out what the grab key is for from what the player is looking at.</summary>
internal static class Context
{
    private static readonly AccessTools.FieldRef<Hud, Piece> HoveredPiece = AccessTools.FieldRefAccess<Hud, Piece>("m_hoveredPiece");
    private static readonly FieldInfo SelectedRecipe = AccessTools.Field(typeof(InventoryGui), "m_selectedRecipe");
    private static PropertyInfo _recipeOf;
    private static PropertyInfo _upgradeOf;

    /// <summary>
    /// The recipe selected in the crafting tab, else the build piece under the cursor in the
    /// build menu, else the piece being placed.
    /// </summary>
    public static Target Find(Player player)
    {
        if (InventoryGui.IsVisible() && InventoryGui.instance && Recipe(player) is Target recipe)
        {
            return recipe;
        }

        Piece piece = null;
        if (Hud.IsPieceSelectionVisible() && Hud.instance)
        {
            piece = HoveredPiece(Hud.instance);
        }

        if (!piece && player.InPlaceMode())
        {
            piece = player.GetSelectedPiece();
        }

        return piece ? Target.ForPiece(piece) : null;
    }

    /// <summary>
    /// The selected recipe at the quality the crafting tab would make: a new item at quality 1,
    /// an upgrade at the next level. A new item is made as many times as the crafting tab's
    /// amount says; an upgrade once.
    /// </summary>
    private static Target Recipe(Player player)
    {
        var gui = InventoryGui.instance;
        if (SelectedRecipe == null || !(gui.InCraftTab() || gui.InUpradeTab()))
        {
            return null;
        }

        object pair = SelectedRecipe.GetValue(gui);
        _recipeOf ??= pair.GetType().GetProperty("Recipe");
        _upgradeOf ??= pair.GetType().GetProperty("ItemData");
        if (_recipeOf?.GetValue(pair) is not Recipe recipe || !recipe || !recipe.m_item)
        {
            return null;
        }

        var upgrade = _upgradeOf?.GetValue(pair) as ItemDrop.ItemData;
        int quality = upgrade == null ? 1 : upgrade.m_quality + 1;
        int count = upgrade == null ? CraftAmountLink.Current() : 1;
        return Target.ForRecipe(recipe, quality, player.GetCurrentCraftingStation(), player, count);
    }
}

/// <summary>Names as the player reads them.</summary>
internal static class Text
{
    public static string Shown(string token) => Localization.instance != null ? Localization.instance.Localize(token) : token;
}
