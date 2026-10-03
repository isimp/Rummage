using System;
using System.Reflection;
using BepInEx.Bootstrap;

namespace Rummage.Game;

/// <summary>
/// MultiUserChest carries a take from a chest another player's game holds: the take is asked of
/// that game, which removes the items and sends them back. Rummage hands it the take exactly as
/// the inventory screen does, and uses these checks to be sure it will be carried that way
/// before handing it over.
/// </summary>
internal static class MucLink
{
    public const string Guid = "com.maxsch.valheim.MultiUserChest";

    private static object _itemOwners;
    private static MethodInfo _tryGetOwner;
    private static MethodInfo _blockOf;
    private static MethodInfo _isSlotBlocked;
    private static MethodInfo _ignores;

    /// <summary>Whether MultiUserChest is loaded and understood, so other players' chests can be used.</summary>
    public static bool Ready { get; private set; }

    public static void Init()
    {
        if (!Chainloader.PluginInfos.TryGetValue(Guid, out var info) || info.Instance == null)
        {
            Plugin.Log.LogInfo("MultiUserChest is not loaded, so chests another player's game holds are left alone.");
            return;
        }

        try
        {
            var assembly = info.Instance.GetType().Assembly;
            const BindingFlags any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

            _itemOwners = assembly.GetType("MultiUserChest.Patches.InventoryPatch", true).GetField("InventoryOfItem", any)?.GetValue(null);
            _tryGetOwner = _itemOwners?.GetType().GetMethod("TryGetValue");

            var block = assembly.GetType("MultiUserChest.InventoryBlock", true);
            _blockOf = block.GetMethod("Get", any, null, [typeof(Inventory)], null);
            _isSlotBlocked = block.GetMethod("IsSlotBlocked", BindingFlags.Public | BindingFlags.Instance, null, [typeof(Vector2i)], null);

            _ignores = assembly.GetType("MultiUserChest.InventoryIgnore", true).GetMethod("IgnoreInventory", any, null, [typeof(Container)], null);

            Ready = _tryGetOwner != null && _blockOf != null && _isSlotBlocked != null && _ignores != null;
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"MultiUserChest {info.Metadata.Version} could not be read ({e.GetType().Name}: {e.Message}).");
        }

        if (!Ready)
        {
            Plugin.Log.LogWarning("MultiUserChest was found but not understood, so chests another player's game holds are left alone.");
        }
    }

    /// <summary>Whether MultiUserChest carries takes from this chest at all.</summary>
    public static bool Carries(Container chest, ZNetView view)
    {
        if (!Ready || !view || !view.IsValid() || !view.HasOwner())
        {
            return false;
        }

        try
        {
            return _ignores.Invoke(null, [chest]) is false;
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"MultiUserChest's chest check failed: {e.Message}");
            return false;
        }
    }

    /// <summary>Whether MultiUserChest knows the item as being in this chest, which it needs to carry the take.</summary>
    public static bool Knows(Container chest, ItemDrop.ItemData item)
    {
        try
        {
            object[] args = [item, null];
            return _tryGetOwner.Invoke(_itemOwners, args) is true && ReferenceEquals(args[1], chest.GetInventory());
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"MultiUserChest's item check failed: {e.Message}");
            return false;
        }
    }

    /// <summary>Whether an inventory slot is still held for a take that has not been answered.</summary>
    public static bool IsWaiting(Inventory bag, int x, int y)
    {
        try
        {
            object block = _blockOf.Invoke(null, [bag]);
            return _isSlotBlocked.Invoke(block, [new Vector2i(x, y)]) is true;
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"MultiUserChest's slot check failed: {e.Message}");
            return false;
        }
    }
}
