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

    public static Scene Read(Player player)
    {
        var sources = new List<Source>();
        var chests = new Dictionary<int, Container>();
        var here = player.transform.position;

        foreach (var chest in CraftyLink.NearbyChests(player))
        {
            var inventory = chest.GetInventory();
            var view = View(chest);
            if (inventory == null || !view || !view.IsValid())
            {
                continue;
            }

            int id = chests.Count;
            chests[id] = chest;
            string chestPrefab = Utils.GetPrefabName(chest.gameObject);
            sources.Add(new Source(id, Vector3.Distance(here, chest.transform.position), ReachOf(chest, view),
                Stacks(inventory), itemPrefab => CraftyLink.MayTake(chestPrefab, itemPrefab)));
        }

        var bag = player.GetInventory();
        return new Scene(sources, chests, new Bag(bag.GetWidth(), bag.GetHeight(), Stacks(bag), ExtraSlotsLink.IsOrdinarySlot));
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
