using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Rummage.Core;

namespace Rummage.Game;

/// <summary>What a grab is for: what one of it costs, and how many.</summary>
internal sealed class Target
{
    public Target(string shown, EntryKind kind, IReadOnlyList<(string Name, int Amount)> cost, int count, bool anyOf, ItemStack into = null, UnityEngine.Sprite icon = null)
    {
        Into = into;
        Icon = icon ? icon : (cost.Count > 0 ? UiArt.ItemIcon(cost[0].Name) : null);
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

    /// <summary>For a top-up, the carried stack to fill; otherwise none.</summary>
    public ItemStack Into { get; }

    /// <summary>What the search box shows beside it.</summary>
    public UnityEngine.Sprite Icon { get; }

    /// <summary>Whether this tops a stack up to a full one.</summary>
    public bool FillsStack => Into != null && Count >= Into.MaxStack - Into.Count;

    public string Label => FillsStack ? $"{Shown} to a full stack" : Count > 1 ? $"{Shown} x{Count}" : Shown;

    public Target WithCount(int count) => new(Shown, Kind, Cost, count, AnyOf, Into, Icon);

    /// <summary>
    /// What to ask the chests for. A piece or recipe is a total to carry, so what the inventory
    /// already holds counts; an item is a number to bring.
    /// </summary>
    public List<Want> Wants() => Into != null ? [Core.Want.TopUp(Into).WithCount(Count)] : Cost.Select(Want).ToList();

    public Want Want((string Name, int Amount) part)
        => Kind == EntryKind.New
            ? NewItems.Want(part.Name)
            : new(part.Name, part.Amount * Count, countCarried: Kind != EntryKind.Item, minWorldLevel: global::Game.m_worldLevel);

    /// <summary>One of each of these items, which the player has never carried; the count stays one.</summary>
    public static Target ForNew(IEnumerable<string> names)
        => new(NewItems.Label, EntryKind.New, names.Select(name => (name, 1)).ToList(), 1, anyOf: false);

    /// <summary>Fills a carried stack, by at most <paramref name="atMost"/> when it is above zero, else to a full stack.</summary>
    public static Target ForTopUp(ItemStack held, int atMost = 0)
        => new(Text.Shown(held.Name), EntryKind.Item, [(held.Name, 1)], Core.Want.TopUp(held, atMost).Count, anyOf: false, held);

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

        return new Target(Text.Shown(piece.m_name), EntryKind.Piece, cost, count, anyOf: false, icon: piece.m_icon);
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

        return new Target(Text.Shown(recipe.m_item.m_itemData.m_shared.m_name), EntryKind.Recipe, cost, count, recipe.m_requireOnlyOneIngredient,
            icon: recipe.m_item.m_itemData.GetIcon());
    }
}

/// <summary>Works out what the grab key is for from what the player is looking at.</summary>
internal static class Context
{
    private static readonly AccessTools.FieldRef<Hud, Piece> HoveredPiece = AccessTools.FieldRefAccess<Hud, Piece>("m_hoveredPiece");
    private static readonly FieldInfo SelectedRecipe = AccessTools.Field(typeof(InventoryGui), "m_selectedRecipe");
    private static PropertyInfo _recipeOf;
    private static PropertyInfo _upgradeOf;

    private static readonly MethodInfo HoveredElement = AccessTools.Method(typeof(InventoryGrid), "GetHoveredElement");
    private static readonly MethodInfo ElementPos = AccessTools.Method(typeof(InventoryGrid), "GetElementPos");

    /// <summary>
    /// With the inventory open: a stack of yours under the cursor, else the recipe selected in
    /// the crafting tab. Otherwise the build piece under the cursor in the build menu, else the
    /// piece being placed.
    /// </summary>
    public static Target Find(Player player) => Find(player, hovered: true);

    /// <summary>
    /// As <see cref="Find(Player)"/>; the self-test leaves out the stack under the cursor, which it
    /// cannot place. What the settings turn off is passed over.
    /// </summary>
    internal static Target Find(Player player, bool hovered)
    {
        if (InventoryGui.IsVisible() && InventoryGui.instance)
        {
            if (hovered && Settings.TopUpStacks.Value && HoveredStack(player) is ItemDrop.ItemData held)
            {
                return Target.ForTopUp(Scene.StackOf(held), Settings.TopUpAmount.Value);
            }

            if (Settings.TakeForRecipes.Value && Recipe(player) is Target recipe)
            {
                return OneMore(recipe, player);
            }
        }

        return Settings.TakeForPieces.Value ? PieceTarget(player) : null;
    }

    /// <summary>The build piece under the cursor in the build menu, else the one being placed, for the next press.</summary>
    public static Target PieceTarget(Player player, Scene scene = null)
    {
        var piece = PieceInView(player);
        return piece ? OneMore(Target.ForPiece(piece), player, scene) : null;
    }

    /// <summary>The build piece under the cursor in the build menu, else the one being placed, as the build panel shows it.</summary>
    public static Piece PieceInView(Player player)
    {
        Piece piece = null;
        if (Hud.IsPieceSelectionVisible() && Hud.instance)
        {
            piece = HoveredPiece(Hud.instance);
        }

        if (!piece && player.InPlaceMode())
        {
            piece = player.GetSelectedPiece();
        }

        return piece;
    }

    /// <summary>
    /// Each press is for one more than what is carried already pays for: one piece more, or one
    /// more round of the crafting tab's craft amount. A part the chests cannot give any more of
    /// does not hold the count down.
    /// </summary>
    private static Target OneMore(Target target, Player player, Scene scene = null)
    {
        var takeable = Stock.Takeable((scene ?? Scene.Read(player)).Sources, CraftyLink.LeaveOne, global::Game.m_worldLevel);
        return target.WithCount(Repeat.Next(Cost.Sum(target.Cost), Scene.Carried(player), takeable, target.Count, target.AnyOf));
    }

    /// <summary>
    /// A stack in the player's inventory under the cursor, found the way the inventory grid finds
    /// it; only an item that stacks, as a single one has nothing to fill.
    /// </summary>
    private static ItemDrop.ItemData HoveredStack(Player player)
    {
        var grid = InventoryGui.instance.m_playerGrid;
        if (!grid || HoveredElement == null || ElementPos == null)
        {
            return null;
        }

        object element = HoveredElement.Invoke(grid, null);
        if (element == null || ElementPos.Invoke(grid, [element]) is not Vector2i at || at.x < 0)
        {
            return null;
        }

        var item = player.GetInventory().GetItemAt(at.x, at.y);
        return item?.m_shared != null && item.m_shared.m_maxStackSize > 1 ? item : null;
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
    /// <summary>The localized name without any markup a mod wrote into it, such as a colour.</summary>
    public static string Shown(string token) => Names.Plain(Localization.instance != null ? Localization.instance.Localize(token) : token);
}
