using System.Collections.Generic;

namespace Rummage.Game;

/// <summary>
/// Where Rummage's lines are shown: in the corner as the game's own messages, or, while the
/// inventory is open and covers the corner, as one message in the middle of the screen.
/// </summary>
internal static class Notice
{
    public static void Say(Player player, IReadOnlyList<string> lines)
    {
        if (!player || lines == null || lines.Count == 0)
        {
            return;
        }

        if (InventoryGui.IsVisible())
        {
            player.Message(MessageHud.MessageType.Center, string.Join("\n", lines));
            return;
        }

        foreach (string line in lines)
        {
            player.Message(MessageHud.MessageType.TopLeft, line);
        }
    }

    public static void Say(Player player, string line) => Say(player, [line]);
}