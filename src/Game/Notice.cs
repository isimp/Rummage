using System.Collections.Generic;
using Rummage.Core;

namespace Rummage.Game;

/// <summary>
/// Where Rummage's lines are shown: in the corner as the game's own messages, or, while the
/// inventory is open and covers the corner, as one message in the middle of the screen.
/// </summary>
internal static class Notice
{
    public static void Say(Player player, IReadOnlyList<string> lines)
    {
        if (!player)
        {
            return;
        }

        var (centre, corner) = Placement.Of(InventoryGui.IsVisible(), lines);
        if (centre != null)
        {
            player.Message(MessageHud.MessageType.Center, centre);
        }

        foreach (string line in corner)
        {
            player.Message(MessageHud.MessageType.TopLeft, line);
        }
    }

    public static void Say(Player player, string line) => Say(player, [line]);
}