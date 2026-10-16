using System;
using System.Collections.Generic;
using System.Linq;

namespace Rummage.Core;

/// <summary>How a press for a build piece counts what the inventory holds.</summary>
internal enum PiecePress
{
    /// <summary>What the inventory holds counts towards the piece; a press brings what one more still needs.</summary>
    TopUp,

    /// <summary>Every press brings the piece's whole cost, whatever the inventory holds, so presses for different pieces add up.</summary>
    FullSet,
}

/// <summary>What a press of the key, or a count typed in the search box, asks the chests for.</summary>
internal static class Presses
{
    /// <summary>
    /// Whether what the inventory holds counts towards what is asked for. A recipe is a total to
    /// carry; a piece is one too unless <see cref="PiecePress.FullSet"/> is chosen; an item is a
    /// number to bring.
    /// </summary>
    public static bool CountsCarried(EntryKind kind, PiecePress rule) => kind switch
    {
        EntryKind.Piece => rule == PiecePress.TopUp,
        EntryKind.Recipe => true,
        _ => false,
    };

    /// <summary>
    /// How many the next press is for: under <see cref="PiecePress.FullSet"/> a piece press is
    /// for one more, otherwise one more than what is carried already pays for (see <see cref="Repeat"/>).
    /// </summary>
    public static int Next(EntryKind kind, PiecePress rule, IReadOnlyDictionary<string, int> cost, IReadOnlyDictionary<string, int> carried,
        IReadOnlyDictionary<string, int> takeable, int step, bool anyOf)
        => CountsCarried(kind, rule) ? Repeat.Next(cost, carried, takeable, step, anyOf) : Math.Max(1, step);

    /// <summary>What <paramref name="count"/> of something costing <paramref name="cost"/> asks the chests for.</summary>
    public static List<Want> Wants(IEnumerable<(string Name, int Amount)> cost, int count, EntryKind kind, PiecePress rule, int worldLevel)
    {
        bool countsCarried = CountsCarried(kind, rule);
        return cost.Select(part => new Want(part.Name, part.Amount * count, countsCarried, worldLevel)).ToList();
    }
}
