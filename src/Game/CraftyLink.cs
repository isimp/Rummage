using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using UnityEngine;

namespace Rummage.Game;

/// <summary>
/// AzuCraftyBoxes decides which chests Rummage may use and what may leave them: its list of
/// nearby chests (privacy, wards, chests open elsewhere, its own toggles), its per-chest item
/// rules, its range and its "Leave One Item" setting. All of it is read from AzuCraftyBoxes
/// itself, through its public API and its own config.
/// </summary>
internal static class CraftyLink
{
    public const string Guid = "Azumatt.AzuCraftyBoxes";

    private const string Section = "2 - CraftyBoxes";

    private static readonly Dictionary<Type, FieldInfo> ContainerFields = new();

    private static MethodInfo _nearby;
    private static MethodInfo _canPull;
    private static HashSet<Container> _registry;
    private static ConfigEntryBase _leaveOne;
    private static ConfigEntryBase _range;

    /// <summary>The self-test narrows the chests to its own while it runs; null otherwise.</summary>
    internal static Func<Container, bool> OnlyChests { get; set; }

    /// <summary>Whether AzuCraftyBoxes was found and understood; without it Rummage does nothing.</summary>
    public static bool Ready { get; private set; }

    /// <summary>What the player is told when the key is pressed and AzuCraftyBoxes is not there to be used.</summary>
    public static string Problem { get; private set; } = "Rummage needs AzuCraftyBoxes, which is not installed";

    public static bool LeaveOne => string.Equals(_leaveOne?.BoxedValue?.ToString(), "On", StringComparison.Ordinal);

    public static float Range => _range?.BoxedValue is float range ? range : 20f;

    public static void Init()
    {
        if (!Chainloader.PluginInfos.TryGetValue(Guid, out var info) || info.Instance == null)
        {
            Plugin.Log.LogError("AzuCraftyBoxes is not loaded, so Rummage has no chests to search.");
            return;
        }

        try
        {
            var api = info.Instance.GetType().Assembly.GetType("AzuCraftyBoxes.API", throwOnError: true);
            _nearby = api.GetMethod("GetNearbyContainers", BindingFlags.Public | BindingFlags.Static)
                .MakeGenericMethod(typeof(Player));
            _canPull = api.GetMethod("CanItemBePulled", BindingFlags.Public | BindingFlags.Static, null, [typeof(string), typeof(string)], null);
            _registry = info.Instance.GetType().Assembly.GetType("AzuCraftyBoxes.Util.Functions.Boxes")?
                .GetField("Containers", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) as HashSet<Container>;

            var config = info.Instance.Config;
            _leaveOne = Entry(config, "Leave One Item");
            _range = Entry(config, "Container Range");

            Ready = _nearby != null && _canPull != null;
            if (_leaveOne == null || _range == null)
            {
                Plugin.Log.LogWarning("AzuCraftyBoxes' Leave One Item or Container Range setting was not found; Rummage takes it as off and 20 m.");
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"AzuCraftyBoxes {info.Metadata.Version} could not be read ({e.GetType().Name}: {e.Message}), so Rummage has no chests to search.");
        }

        if (!Ready)
        {
            Problem = $"Rummage could not read AzuCraftyBoxes {info.Metadata.Version}; see the log";
        }

        if (!Ready && _nearby == null)
        {
            Plugin.Log.LogError("AzuCraftyBoxes' chest list was not found, so Rummage has no chests to search.");
        }
    }

    private static ConfigEntryBase Entry(ConfigFile config, string key)
    {
        var definition = new ConfigDefinition(Section, key);
        return config.ContainsKey(definition) ? config[definition] : null;
    }

    /// <summary>The chests AzuCraftyBoxes would let the player pull from right now.</summary>
    public static List<Container> NearbyChests(Player player)
    {
        var chests = new List<Container>();
        if (!Ready)
        {
            return chests;
        }

        try
        {
            if (_nearby.Invoke(null, [player, Range]) is IEnumerable found)
            {
                foreach (object entry in found)
                {
                    if (entry != null && ContainerOf(entry) is Container chest && chest && (OnlyChests == null || OnlyChests(chest)))
                    {
                        chests.Add(chest);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"AzuCraftyBoxes' chest list failed: {e.GetType().Name}: {e.Message}");
        }

        return chests;
    }

    /// <summary>Whether AzuCraftyBoxes' registry of every chest could be read.</summary>
    public static bool HasRegistry => _registry != null;

    /// <summary>
    /// Every chest AzuCraftyBoxes has registered within range, whether or not it lets the player
    /// use it now, read only; empty when the registry cannot be read.
    /// </summary>
    public static IEnumerable<Container> Registered(Vector3 here, float range)
    {
        if (_registry == null)
        {
            yield break;
        }

        float reach = range * range;
        foreach (var chest in new List<Container>(_registry))
        {
            if (chest && (chest.transform.position - here).sqrMagnitude <= reach)
            {
                yield return chest;
            }
        }
    }

    /// <summary>Whether the chest's rules let the item, by prefab name, leave it.</summary>
    public static bool MayTake(string chestPrefab, string itemPrefab)
    {
        try
        {
            return _canPull.Invoke(null, [chestPrefab, itemPrefab]) is true;
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"AzuCraftyBoxes' item rules failed for {itemPrefab} in {chestPrefab}: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// AzuCraftyBoxes lists vanilla chests, item drawers and the player's own bags as one kind; a
    /// vanilla chest is the entry that holds a Container. The others are not chests to search.
    /// </summary>
    private static Container ContainerOf(object entry)
    {
        var type = entry.GetType();
        if (!ContainerFields.TryGetValue(type, out var field))
        {
            field = Array.Find(type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic), f => f.FieldType == typeof(Container));
            ContainerFields[type] = field;
        }

        return field?.GetValue(entry) as Container;
    }
}
