using System.Collections.Generic;
using System.Linq;

namespace Rummage.Core;

/// <summary>
/// The pings on the map: one per chest, each taken down once its time is up. Pinging a chest
/// again replaces its ping. The chest and pin types are parameters, so the tests can use their
/// own; the game side pings the game's chests with the map's own pins.
/// </summary>
internal sealed class Pings<TChest, TPin> where TChest : class where TPin : class
{
    private readonly Dictionary<TChest, (TPin Pin, float Until)> _pinned = new();
    private readonly List<TPin> _due = new();
    private readonly List<TChest> _done = new();

    /// <summary>How many pings are up.</summary>
    public int Count => _pinned.Count;

    /// <summary>The chest's ping, or null when it has none up.</summary>
    public TPin PinOf(TChest chest) => _pinned.TryGetValue(chest, out var pinned) ? pinned.Pin : null;

    /// <summary>Pings a chest until <paramref name="until"/>, and gives back the ping it replaces, if any, to take down.</summary>
    public TPin Put(TChest chest, TPin pin, float until)
    {
        var replaced = _pinned.TryGetValue(chest, out var was) ? was.Pin : null;
        _pinned[chest] = (pin, until);
        return replaced;
    }

    /// <summary>The pings whose time is up, to take down; they are no longer held. The list is reused by the next call.</summary>
    public IReadOnlyList<TPin> Due(float now)
    {
        _due.Clear();
        foreach (var pinned in _pinned)
        {
            if (now >= pinned.Value.Until)
            {
                _done.Add(pinned.Key);
                _due.Add(pinned.Value.Pin);
            }
        }

        foreach (var chest in _done)
        {
            _pinned.Remove(chest);
        }

        _done.Clear();
        return _due;
    }

    /// <summary>Every ping, which is no longer held: for when the map they are on goes.</summary>
    public IReadOnlyList<TPin> TakeAll()
    {
        var all = _pinned.Values.Select(p => p.Pin).ToList();
        _pinned.Clear();
        return all;
    }
}
