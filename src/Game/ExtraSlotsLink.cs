using System;
using System.Reflection;
using BepInEx.Bootstrap;

namespace Rummage.Game;

/// <summary>
/// ExtraSlots adds equipment, quick, food and ammo slots below the inventory grid. Rummage
/// never puts what it takes into one of them; ExtraSlots says which positions they are.
/// </summary>
internal static class ExtraSlotsLink
{
    public const string Guid = "shudnal.ExtraSlots";

    private static MethodInfo _isSlot;

    public static void Init()
    {
        if (!Chainloader.PluginInfos.TryGetValue(Guid, out var info) || info.Instance == null)
        {
            return;
        }

        try
        {
            _isSlot = info.Instance.GetType().Assembly.GetType("ExtraSlots.API", true)
                .GetMethod("IsGridPositionASlot", BindingFlags.Public | BindingFlags.Static, null, [typeof(Vector2i)], null);
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"ExtraSlots could not be read: {e.Message}");
        }

        if (_isSlot == null)
        {
            Plugin.Log.LogWarning("ExtraSlots was found but not understood, so Rummage only fills the inventory's first four rows.");
        }
    }

    /// <summary>Whether ordinary items may be placed at the position.</summary>
    public static bool IsOrdinarySlot(int x, int y)
    {
        if (!Chainloader.PluginInfos.ContainsKey(Guid))
        {
            return true;
        }

        if (_isSlot == null)
        {
            return y < 4;
        }

        try
        {
            return _isSlot.Invoke(null, [new Vector2i(x, y)]) is false;
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"ExtraSlots' slot check failed: {e.Message}");
            _isSlot = null;
            return y < 4;
        }
    }
}
