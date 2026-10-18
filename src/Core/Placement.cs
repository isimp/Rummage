using System;
using System.Collections.Generic;

namespace Rummage.Core;

/// <summary>Where Rummage's lines are shown, with no game in it; the game side shows them there.</summary>
internal static class Placement
{
    /// <summary>
    /// The lines in the corner as the game's own messages, or, while the inventory is open and
    /// covers the corner, together as one message in the middle of the screen. No lines, nothing.
    /// </summary>
    public static (string Centre, IReadOnlyList<string> Corner) Of(bool inventoryOpen, IReadOnlyList<string> lines)
    {
        if (lines == null || lines.Count == 0)
        {
            return (null, Array.Empty<string>());
        }

        return inventoryOpen ? (string.Join("\n", lines), Array.Empty<string>()) : (null, lines);
    }
}
