using System.Collections.Generic;
using UnityEngine;

namespace Rummage.Game;

/// <summary>
/// Keeps chests lit with the game's own highlight, the glow a piece gets under the cursor while
/// building. The game's highlight fades after a fifth of a second, so it is given again every
/// tenth of a second for as long as asked.
/// </summary>
internal static class Highlighter
{
    private const float Every = 0.1f;

    private static readonly Dictionary<WearNTear, float> Until = new();
    private static readonly List<WearNTear> Done = new();
    private static float _next;

    public static void Light(Container chest, float seconds)
    {
        if (!chest)
        {
            return;
        }

        var root = chest.m_rootObjectOverride ? chest.m_rootObjectOverride.gameObject : chest.gameObject;
        if (root.TryGetComponent(out WearNTear piece))
        {
            float end = Time.time + seconds;
            Until[piece] = Until.TryGetValue(piece, out float was) ? Mathf.Max(was, end) : end;
            piece.Highlight();
        }
    }

    public static void Tick(float now)
    {
        if (Until.Count == 0 || now < _next)
        {
            return;
        }

        _next = now + Every;
        foreach (var lit in Until)
        {
            if (!lit.Key || now >= lit.Value)
            {
                Done.Add(lit.Key);
            }
            else
            {
                lit.Key.Highlight();
            }
        }

        foreach (var piece in Done)
        {
            Until.Remove(piece);
        }

        Done.Clear();
    }
}
