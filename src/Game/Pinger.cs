using System.Collections.Generic;
using HarmonyLib;
using Rummage.Core;
using UnityEngine;

namespace Rummage.Game;

/// <summary>
/// Pings chests on the map, the small one and the large one, with the game's own ping icon, at
/// the size and pulse of a ping a player sends. A ping is a pin of this game alone: it is never
/// sent to other players and never saved with the map, and it is taken down after
/// <c>PingSeconds</c>. The map redraws its pins only when told to, so it is told every frame
/// while a ping is up, as the game does for its own pings.
/// </summary>
internal static class Pinger
{
    private static readonly AccessTools.FieldRef<Minimap, bool> RedrawPins = AccessTools.FieldRefAccess<Minimap, bool>("m_pinUpdateRequired");

    private static readonly Pings<Container, Minimap.PinData> Up = new();
    private static Minimap _map;

    /// <summary>The chests the last ping marked, for the self-test.</summary>
    internal static IReadOnlyList<Container> LastPinged { get; private set; } = [];

    /// <summary>The pin of a chest's ping, for the self-test; null when it has none up.</summary>
    internal static Minimap.PinData PinOf(Container chest) => Up.PinOf(chest);

    /// <summary>Pings each chest, named by what it holds of what was looked for.</summary>
    public static void Ping(IEnumerable<(Container Chest, string Name)> chests)
    {
        var pinged = new List<Container>();
        float seconds = Settings.PingSeconds.Value;
        var map = Follow();
        if (seconds > 0f && map)
        {
            foreach (var (chest, name) in chests)
            {
                if (!chest)
                {
                    continue;
                }

                var pin = map.AddPin(chest.transform.position, Minimap.PinType.Ping, name, save: false, isChecked: false);
                pin.m_doubleSize = true;
                pin.m_animate = true;
                var replaced = Up.Put(chest, pin, Time.time + seconds);
                if (replaced != null)
                {
                    map.RemovePin(replaced);
                }

                pinged.Add(chest);
            }
        }

        LastPinged = pinged;
    }

    public static void Tick(float now)
    {
        var map = Follow();
        if (!map || Up.Count == 0)
        {
            return;
        }

        foreach (var pin in Up.Due(now))
        {
            map.RemovePin(pin);
        }

        RedrawPins(map) = true;
    }

    /// <summary>The map now; when it is another than before, the pings went with the one before.</summary>
    private static Minimap Follow()
    {
        var map = Minimap.instance;
        if (map != _map)
        {
            Up.TakeAll();
            _map = map;
        }

        return map;
    }
}
