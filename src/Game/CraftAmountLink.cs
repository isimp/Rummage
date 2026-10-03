using System;
using System.Reflection;
using BepInEx.Bootstrap;

namespace Rummage.Game;

/// <summary>
/// AzuAntiArthriticCrafting adds an amount to the crafting tab and crafts that many at once; a
/// grab for the selected recipe covers the same amount, read from the mod itself. Without it the
/// game crafts one at a time.
/// </summary>
internal static class CraftAmountLink
{
    public const string Guid = "Azumatt.AzuAntiArthriticCrafting";

    private static MethodInfo _current;

    public static void Init()
    {
        if (!Chainloader.PluginInfos.TryGetValue(Guid, out var info) || info.Instance == null)
        {
            return;
        }

        try
        {
            _current = info.Instance.GetType().Assembly.GetType("AzuAntiArthriticCrafting.Patches.HaveRequirementItemsTranspiler", true)
                .GetMethod("GetCurrentCraftAmount", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"AzuAntiArthriticCrafting could not be read: {e.Message}");
        }

        if (_current == null)
        {
            Plugin.Log.LogWarning("AzuAntiArthriticCrafting's craft amount was not found, so a recipe grab is for one craft.");
        }
    }

    public static int Current()
    {
        if (_current == null)
        {
            return 1;
        }

        try
        {
            return _current.Invoke(null, null) is int amount && amount > 0 ? amount : 1;
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"AzuAntiArthriticCrafting's craft amount failed: {e.Message}");
            _current = null;
            return 1;
        }
    }
}
