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

    public static void Light(Container chest, float seconds)
    {
        if (!chest)
        {
            return;
        }

        var root = chest.m_rootObjectOverride ? chest.m_rootObjectOverride.gameObject : chest.gameObject;
        if (root.TryGetComponent(out WearNTear piece))
        {
            Lit.Light(piece, Time.time + seconds);
            piece.Highlight();
        }
    }

    public static void Tick(float now)
    {
        foreach (var piece in Lit.Due(now))
        {
            piece.Highlight();
        }
    }
}
