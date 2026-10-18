using System;
using System.Collections.Generic;

namespace Rummage.Core;

/// <summary>
/// Which pieces are kept lit with the game's highlight, which fades after a fifth of a second: a
/// lit piece is lit again every tenth of a second until its time is up or it is gone. The piece
/// type is a parameter, so the tests can use their own; the game side lights the game's pieces.
/// </summary>
internal sealed class Lights<TPiece> where TPiece : class
{
    private const float Every = 0.1f;

    private readonly Dictionary<TPiece, float> _until = new();
    private readonly List<TPiece> _due = new();
    private readonly List<TPiece> _done = new();
    private readonly Func<TPiece, bool> _gone;
    private float _next;

    /// <param name="gone">Whether a piece no longer exists.</param>
    public Lights(Func<TPiece, bool> gone)
    {
        _gone = gone;
    }

    /// <summary>How many pieces are lit.</summary>
    public int Count => _until.Count;

    /// <summary>Keeps a piece lit until <paramref name="until"/>, or longer when it is lit for longer already.</summary>
    public void Light(TPiece piece, float until)
    {
        _until[piece] = _until.TryGetValue(piece, out float was) ? Math.Max(was, until) : until;
    }

    /// <summary>
    /// The pieces to light again now, every tenth of a second; none between. A piece whose time
    /// is up or that is gone is no longer lit. The list is reused by the next call.
    /// </summary>
    public IReadOnlyList<TPiece> Due(float now)
    {
        _due.Clear();
        if (_until.Count == 0 || now < _next)
        {
            return _due;
        }

        _next = now + Every;
        foreach (var lit in _until)
        {
            if (_gone(lit.Key) || now >= lit.Value)
            {
                _done.Add(lit.Key);
            }
            else
            {
                _due.Add(lit.Key);
            }
        }

        foreach (var piece in _done)
        {
            _until.Remove(piece);
        }

        _done.Clear();
        return _due;
    }
}
