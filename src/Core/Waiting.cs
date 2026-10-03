using System;
using System.Collections.Generic;
using System.Linq;

namespace Rummage.Core;

/// <summary>
/// The takes of the last grab that went through other players' games and have no answer yet,
/// each known by the inventory slot it fills. A new grab starts only once they are answered, or
/// once they have waited past the timeout.
/// </summary>
internal sealed class Waiting
{
    private readonly float _timeout;
    private readonly HashSet<(int X, int Y)> _open = new();
    private readonly List<(int X, int Y)> _unanswered = new();
    private float _started;

    public Waiting(float timeoutSeconds)
    {
        _timeout = timeoutSeconds;
    }

    public bool Busy => _open.Count > 0;

    /// <summary>Takes the last grab gave up waiting for.</summary>
    public IReadOnlyList<(int X, int Y)> Unanswered => _unanswered;

    public void Start(IEnumerable<(int X, int Y)> slots, float now)
    {
        _open.Clear();
        _unanswered.Clear();
        _open.UnionWith(slots);
        _started = now;
    }

    /// <summary>Settles every take whose slot is no longer waiting; returns how many did.</summary>
    public int Check(Func<(int X, int Y), bool> stillWaiting, float now)
    {
        int answered = _open.RemoveWhere(slot => !stillWaiting(slot));
        if (_open.Count > 0 && now - _started >= _timeout)
        {
            _unanswered.AddRange(_open.OrderBy(s => s.Y).ThenBy(s => s.X));
            _open.Clear();
        }

        return answered;
    }
}
