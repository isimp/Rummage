using System.Collections.Generic;

namespace Rummage.Core;

/// <summary>What the grab key can be for.</summary>
internal enum Aim
{
    /// <summary>The carried stack under the cursor, filled up.</summary>
    TopUp,

    /// <summary>The recipe selected in the crafting tab.</summary>
    Recipe,

    /// <summary>The build piece in view.</summary>
    Piece,
}

/// <summary>What the grab key is for, with no game in it; the game side looks for each in turn.</summary>
internal static class ContextRules
{
    /// <summary>
    /// What to look for, in order, until one is there: with the inventory open, the carried stack
    /// under the cursor, then the recipe selected in the crafting tab; then the build piece in view.
    /// Each is left out while its setting is off, and the stack under the cursor also where it cannot
    /// be pointed at (<paramref name="hovered"/> false).
    /// </summary>
    public static IReadOnlyList<Aim> Order(bool inventoryOpen, bool hovered, bool topUps, bool recipes, bool pieces)
    {
        var order = new List<Aim>(3);
        if (inventoryOpen && hovered && topUps)
        {
            order.Add(Aim.TopUp);
        }

        if (inventoryOpen && recipes)
        {
            order.Add(Aim.Recipe);
        }

        if (pieces)
        {
            order.Add(Aim.Piece);
        }

        return order;
    }
}
