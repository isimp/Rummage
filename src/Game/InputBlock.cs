using System;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace Rummage.Game;

/// <summary>
/// Keeps the game's hands off the keyboard and mouse while the search box is open.
///
/// The game stops moving, looking, attacking, chatting and opening its menu while a text box is
/// up: <c>Player.TakeInput</c>, <c>PlayerController.TakeInput</c>, the mouse capture in
/// <c>GameCamera.UpdateMouseCapture</c>, <c>Chat.Update</c> and the Escape check in
/// <c>Menu.Update</c> all ask <c>TextInput.IsVisible</c>. Answering yes while the search box is
/// open gets all of that at once, and frees the cursor.
///
/// Each patch runs inside one of the game's own methods every frame, so none may ever fail there:
/// its work is in a method of its own, called inside a try. One that fails is logged once and
/// stands aside for the rest of the session.
/// </summary>
[HarmonyPatch(typeof(TextInput), nameof(TextInput.IsVisible))]
internal static class TextInputBlock
{
    private static bool _off;

    private static void Postfix(ref bool __result)
    {
        if (!_off && Blocks.Safely("keeping the game's keys away while the search box is open", Open, ref _off))
        {
            __result = true;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool Open() => Palette.IsOpen;
}

/// <summary>
/// The inventory reads its keys without asking <c>TextInput.IsVisible</c>: it opens and closes on
/// Tab, and while open it closes on Escape and on the use key (E), letters typed into the search
/// box. So while the search box is open, the inventory's update waits; a craft in progress goes
/// on once the search box closes.
/// </summary>
[HarmonyPatch(typeof(InventoryGui), "Update")]
internal static class InventoryKeyBlock
{
    private static bool _off;

    private static bool Prefix()
        => _off || !Blocks.Safely("keeping the inventory from reading what is typed into the search box", Open, ref _off);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool Open() => Palette.IsOpen;
}

/// <summary>
/// The camera zoom reads the mouse wheel whether a text box is up or not. Every wheel read in the
/// game goes through this one method, so while the search box is open it answers nothing and the
/// wheel only scrolls the list.
/// </summary>
[HarmonyPatch(typeof(ZInput), "Internal_GetMouseScrollWheel")]
internal static class WheelBlock
{
    private static bool _off;

    private static void Postfix(ref float __result)
    {
        if (!_off && Blocks.Safely("keeping the mouse wheel from zooming while the search box is open", Open, ref _off))
        {
            __result = 0f;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool Open() => Palette.IsOpen;
}

internal static class Blocks
{
    /// <summary>Asks a patch's question; a failure is logged once and switches that patch off.</summary>
    public static bool Safely(string part, Func<bool> ask, ref bool off)
    {
        try
        {
            return ask();
        }
        catch (Exception e)
        {
            off = true;
            Plugin.Log.LogError($"Stopped {part}: {e.GetType().Name}: {e.Message}");
            return false;
        }
    }
}
