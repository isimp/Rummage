using Rummage.Core;
using UnityEngine;

namespace Rummage.Game;

/// <summary>
/// Keeps chests lit with the game's own highlight, the glow a piece gets under the cursor while
/// building. The game's highlight fades after a fifth of a second, so it is given again every
/// tenth of a second for as long as a caller keeps it lit.
/// </summary>
internal static class Highlighter
{
    private static readonly Lights<WearNTear> Lit = new(piece => !piece);

    /// <summary>Lights a chest for the seconds given; none lights nothing.</summary>
    public static void Light(Container chest, float seconds)
    {
        if (!chest || seconds <= 0f)
        {
            return;
        }

        var piece = PieceOf(chest);
        if (piece)
        {
            Lit.Light(piece, Time.time + seconds);
            piece.Highlight();
        }
    }

    /// <summary>Whether a chest is lit now, for the self-test.</summary>
    internal static bool IsLit(Container chest)
    {
        var piece = chest ? PieceOf(chest) : null;
        return piece && Lit.IsLit(piece, Time.time);
    }

    public static void Tick(float now)
    {
        foreach (var piece in Lit.Due(now))
        {
            piece.Highlight();
        }
    }

    private static WearNTear PieceOf(Container chest)
    {
        var root = chest.m_rootObjectOverride ? chest.m_rootObjectOverride.gameObject : chest.gameObject;
        return root.TryGetComponent(out WearNTear piece) ? piece : null;
    }
}
