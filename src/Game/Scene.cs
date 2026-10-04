using System.Collections.Generic;
using HarmonyLib;
using Rummage.Core;
using UnityEngine;

namespace Rummage.Game;

/// <summary>The chests in reach and the player's inventory, read into the planner's terms.</summary>
internal sealed class Scene
{
    private static readonly AccessTools.FieldRef<Container, ZNetView> ViewOf = AccessTools.FieldRefAccess<Container, ZNetView>("m_nview");

    private Scene(List<Source> sources, Dictionary<int, Container> chests, Bag bag)
    {
        Sources = sources;
        Chests = chests;
        Bag = bag;
    }

    public List<Source> Sources { get; }

    public Dictionary<int, Container> Chests { get; }

    public Bag Bag { get; }

    public static ZNetView View(Container chest) => ViewOf(chest);

    public static Scene Read(Player player) => Read(player, CraftyLink.NearbyChests(player));

    /// <summary>The scene from a list of chests AzuCraftyBoxes already gave.</summary>
    public static Scene Read(Player player, List<Container> nearby)
    {
        var sources = new List<Source>();
        var chests = new Dictionary<int, Container>();
        var here = player.transform.position;

        foreach (var chest in nearby)
        {
            var inventory = chest.GetInventory();
            var view = View(chest);
            if (inventory == null || !view || !view.IsValid())
            {
                continue;
            }

            int id = chests.Count;
            chests[id] = chest;
            float distance = Vector3.Distance(here, chest.transform.position);
            if (!Access.MayOpen(chest, player))
            {
                // Listed by AzuCraftyBoxes from an earlier moment, but not one the player could open now.
                sources.Add(new Source(id, distance, Reach.Barred, Stacks(inventory), mayTake: null, Bar.Locked));
                continue;
            }

            string chestPrefab = Utils.GetPrefabName(chest.gameObject);
            sources.Add(new Source(id, distance, ReachOf(chest, view),
                Stacks(inventory), itemPrefab => CraftyLink.MayTake(chestPrefab, itemPrefab)));
        }

        var bag = player.GetInventory();
        return new Scene(sources, chests, new Bag(bag.GetWidth(), bag.GetHeight(), Stacks(bag), ExtraSlotsLink.IsOrdinarySlot));
    }

    /// <summary>The layers building pieces and carts are on; chests are one of the two.</summary>
    private static readonly int ChestLayers = LayerMask.GetMask("piece", "piece_nonsolid", "vehicle");

    /// <summary>Reused for every scan, so a scan makes no garbage of its own.</summary>
    private static readonly Collider[] Hits = new Collider[4096];

    /// <summary>
    /// Adds the chests in reach that AzuCraftyBoxes does not let the player use, read only to
    /// say why something is missing: one someone else has open or a cart someone pulls, someone
    /// else's private chest, or one kept out for another reason (a ward, a seal, a setting).
    /// Only chests players built count, as AzuCraftyBoxes counts them. AzuCraftyBoxes' own
    /// registry names most of them; one physics query over building pieces and carts finds the
    /// ones it never registered, such as other players' private chests.
    /// </summary>
    public void AddBarred(Player player)
    {
        var listed = new HashSet<Container>(Chests.Values);
        var seen = new HashSet<Container>();
        var here = player.transform.position;
        float range = CraftyLink.Range;
        var candidates = new List<Container>(CraftyLink.Registered(here, range));
        int hits = Physics.OverlapSphereNonAlloc(here, range, Hits, ChestLayers);
        for (int i = 0; i < hits; i++)
        {
            var found = Hits[i] ? Hits[i].GetComponentInParent<Container>() : null;
            Hits[i] = null;
            if (found)
            {
                candidates.Add(found);
            }
        }

        foreach (var chest in candidates)
        {
            if (!chest || !seen.Add(chest) || listed.Contains(chest) || (CraftyLink.OnlyChests != null && !CraftyLink.OnlyChests(chest)))
            {
                continue;
            }

            var piece = chest.GetComponentInParent<Piece>();
            var view = View(chest);
            var inventory = chest.GetInventory();
            if (!piece || piece.GetCreator() == 0 || inventory == null || !view || !view.IsValid())
            {
                continue;
            }

            Sources.Add(new Source(1000 + Sources.Count, Vector3.Distance(here, chest.transform.position), Reach.Barred,
                Stacks(inventory), mayTake: null, BarOf(chest, piece, view, player)));
        }
    }

    private static Bar BarOf(Container chest, Piece piece, ZNetView view, Player player)
    {
        if ((view.GetZDO().GetInt(ZDOVars.s_inUse) == 1 && !chest.IsInUse()) || (chest.m_wagon && chest.m_wagon.InUse()))
        {
            return Bar.InUse;
        }

        return chest.m_privacy == Container.PrivacySetting.Private && piece.GetCreator() != player.GetPlayerID() ? Bar.Private : Bar.Locked;
    }

    /// <summary>
    /// A chest this game holds saves what changes in it. One another game holds is used only
    /// through MultiUserChest, which asks that game; without it the chest is left alone.
    /// </summary>
    public static Reach ReachOf(Container chest, ZNetView view)
    {
        if (view.IsOwner())
        {
            return Reach.Owned;
        }

        return MucLink.Carries(chest, view) ? Reach.Routed : Reach.Closed;
    }

    /// <summary>What the inventory holds that building and crafting count, by item name.</summary>
    public static Dictionary<string, int> Carried(Player player)
    {
        var carried = new Dictionary<string, int>(System.StringComparer.Ordinal);
        foreach (var item in player.GetInventory().GetAllItems())
        {
            if (item?.m_shared != null && item.m_worldLevel >= global::Game.m_worldLevel)
            {
                carried[item.m_shared.m_name] = (carried.TryGetValue(item.m_shared.m_name, out int n) ? n : 0) + item.m_stack;
            }
        }

        return carried;
    }

    /// <summary>An item as the planner sees it.</summary>
    public static ItemStack StackOf(ItemDrop.ItemData item)
    {
        string prefab = item.m_dropPrefab ? item.m_dropPrefab.name : item.m_shared.m_name;
        return new ItemStack(item.m_shared.m_name, prefab, item.m_stack, item.m_shared.m_maxStackSize,
            item.m_gridPos.x, item.m_gridPos.y, item.m_quality, item.m_worldLevel, item.m_shared.m_maxQuality > 1);
    }

    private static List<ItemStack> Stacks(Inventory inventory)
    {
        var stacks = new List<ItemStack>();
        foreach (var item in inventory.GetAllItems())
        {
            if (item?.m_shared == null)
            {
                continue;
            }

            stacks.Add(StackOf(item));
        }

        return stacks;
    }
}
