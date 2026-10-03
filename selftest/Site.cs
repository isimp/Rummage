using System;
using System.Collections.Generic;
using System.Linq;
using Rummage.Game;
using UnityEngine;

namespace Rummage.SelfTest;

/// <summary>
/// The self-test's own place: chests it builds in front of the player and fills with known
/// items, and vault chests holding the player's belongings while the test runs. Everything is
/// put back and taken down when it ends; anything it cannot put back stays in a vault chest and
/// is reported with its place.
/// </summary>
internal sealed class Site
{
    public const string Wood = "$item_wood";
    public const string Stone = "$item_stone";
    public const string Resin = "$item_resin";
    public const string Arrows = "$item_arrow_wood";

    private static readonly int StoringPaused = "storingPaused".GetStableHashCode();

    /// <summary>The creator as the piece holds it; the game reads it from here, set as the game sets it when a piece is placed.</summary>
    private static readonly HarmonyLib.AccessTools.FieldRef<Piece, long> CreatorOf = HarmonyLib.AccessTools.FieldRefAccess<Piece, long>("m_creator");

    private readonly Player _player;
    private readonly Checks _report;
    private readonly List<Container> _vaults = new();
    private readonly List<(Container Vault, int X, int Y, int ToX, int ToY, string Name, int Stack)> _stored = new();
    private readonly Dictionary<ItemDrop.ItemData, int> _kept = new();
    private ItemDrop.ItemData _right;
    private ItemDrop.ItemData _left;
    private int _placed;

    public Site(Player player, Checks report)
    {
        _player = player;
        _report = report;
    }

    /// <summary>A chest of the wood kind with wood, stone and arrows.</summary>
    public Container Main { get; private set; }

    /// <summary>A second chest of the wood kind with wood only.</summary>
    public Container Second { get; private set; }

    /// <summary>A private chest with resin and wood, which AzuCraftyBoxes' default rules keep closed.</summary>
    public Container Private { get; private set; }

    public IEnumerable<Container> Sources => new[] { Main, Second, Private }.Where(c => c);

    public Inventory Bag => _player.GetInventory();

    /// <summary>Builds the chests, fills them and sets the player's belongings aside.</summary>
    public void Build()
    {
        _right = _player.RightItem;
        _left = _player.LeftItem;
        Main = Place("piece_chest_wood");
        Second = Place("piece_chest_wood");
        Private = Place("piece_chest_private");
        Refill();
        SetBelongingsAside();
    }

    /// <summary>Gives the test chests their starting contents again.</summary>
    public void Refill()
    {
        Fill(Main, (Wood, 50), (Stone, 20), (Arrows, 100), (Arrows, 100));
        Fill(Second, (Wood, 50));
        Fill(Private, (Resin, 20), (Wood, 10));
    }

    /// <summary>Takes everything the test put into the inventory back out, and kept stacks back to their size.</summary>
    public void ClearBag()
    {
        foreach (var item in Bag.GetAllItems().ToList())
        {
            if (_kept.TryGetValue(item, out int stack))
            {
                item.m_stack = stack;
            }
            else
            {
                Bag.RemoveItem(item);
            }
        }

    }

    public static ItemDrop.ItemData Make(string prefab, int count)
    {
        var drop = ObjectDB.instance.GetItemPrefab(prefab).GetComponent<ItemDrop>();
        var item = drop.m_itemData.Clone();
        item.m_dropPrefab = drop.gameObject;
        item.m_stack = count;
        item.m_worldLevel = (byte)global::Game.m_worldLevel;
        return item;
    }

    public static string PrefabOf(string name) => name switch
    {
        Wood => "Wood",
        Stone => "Stone",
        Resin => "Resin",
        Arrows => "ArrowWood",
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    public static int Count(Inventory inventory, string name)
        => inventory == null ? 0 : inventory.GetAllItems().Where(i => i.m_shared.m_name == name).Sum(i => i.m_stack);

    /// <summary>What the inventory and the test chests hold of an item together.</summary>
    public int Total(string name) => Count(Bag, name) + Sources.Sum(c => Count(c.GetInventory(), name));

    /// <summary>The first empty slot ordinary items may use.</summary>
    public (int X, int Y) FreeSlot()
    {
        for (int y = 0; y < Bag.GetHeight(); y++)
        {
            for (int x = 0; x < Bag.GetWidth(); x++)
            {
                if (Bag.GetItemAt(x, y) == null && ExtraSlotsLink.IsOrdinarySlot(x, y))
                {
                    return (x, y);
                }
            }
        }

        return (-1, -1);
    }

    public void PutHandsBack()
    {
        foreach (var hand in new[] { _player.RightItem, _player.LeftItem })
        {
            if (hand != null && hand != _right && hand != _left)
            {
                _player.UnequipItem(hand, triggerEquipEffects: false);
            }
        }

        foreach (var hand in new[] { _right, _left })
        {
            if (hand != null && !hand.m_equipped && Bag.ContainsItem(hand))
            {
                _player.EquipItem(hand, triggerEquipEffects: false);
            }
        }
    }

    /// <summary>Puts every belonging back where it was and takes the test chests down.</summary>
    public void TearDown()
    {
        CraftyLink.OnlyChests = null;
        PutHandsBack();
        ClearBag();

        foreach (var (vault, x, y, toX, toY, name, stack) in _stored)
        {
            var item = vault ? vault.GetInventory().GetItemAt(x, y) : null;
            if (item == null || item.m_shared.m_name != name || item.m_stack != stack)
            {
                _report.Fail($"put {stack} {name} back from the vault chest at {Where(vault)}", "it was not where it was put");
                continue;
            }

            if (Bag.GetItemAt(toX, toY) != null)
            {
                _report.Fail($"put {stack} {name} back into slot {toX},{toY}", "the slot was taken");
                continue;
            }

            Bag.MoveItemToThis(vault.GetInventory(), item, item.m_stack, toX, toY);
        }

        foreach (var chest in Sources)
        {
            chest.GetInventory().RemoveAll();
            TakeDown(chest);
        }

        foreach (var vault in _vaults)
        {
            if (vault.GetInventory().NrOfItems() > 0)
            {
                _report.Fail("empty every vault chest", $"the vault chest at {Where(vault)} still holds {vault.GetInventory().NrOfItems()} of your stacks; it is left standing");
            }
            else
            {
                TakeDown(vault);
            }
        }
    }

    public static string Where(Container chest) => chest ? chest.transform.position.ToString("F0", System.Globalization.CultureInfo.InvariantCulture) : "(gone)";

    private void SetBelongingsAside()
    {
        foreach (var item in Bag.GetAllItems().ToList())
        {
            if (item.m_equipped)
            {
                _kept[item] = item.m_stack;
                continue;
            }

            var (vault, x, y) = VaultSlot();
            int fromX = item.m_gridPos.x;
            int fromY = item.m_gridPos.y;
            string name = item.m_shared.m_name;
            int stack = item.m_stack;
            vault.GetInventory().MoveItemToThis(Bag, item, stack, x, y);
            _stored.Add((vault, x, y, fromX, fromY, name, stack));
        }
    }

    private (Container Vault, int X, int Y) VaultSlot()
    {
        foreach (var vault in _vaults)
        {
            var inventory = vault.GetInventory();
            for (int y = 0; y < inventory.GetHeight(); y++)
            {
                for (int x = 0; x < inventory.GetWidth(); x++)
                {
                    if (inventory.GetItemAt(x, y) == null)
                    {
                        return (vault, x, y);
                    }
                }
            }
        }

        _vaults.Add(Place("piece_chest"));
        return (_vaults[_vaults.Count - 1], 0, 0);
    }

    /// <summary>Builds a chest a few steps in front of the player, on what is underfoot, as the player's own.</summary>
    private Container Place(string prefab)
    {
        var forward = _player.transform.forward;
        forward.y = 0f;
        forward = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
        var right = Vector3.Cross(Vector3.up, forward);
        var at = _player.transform.position + forward * 3f + right * ((_placed % 6) - 2.5f) * 1.6f + forward * (_placed / 6) * 1.6f;
        _placed++;

        if (ZoneSystem.instance.GetSolidHeight(at, out float floor, heightMargin: 2))
        {
            at.y = floor;
        }

        var built = UnityEngine.Object.Instantiate(ZNetScene.instance.GetPrefab(prefab), at, Quaternion.LookRotation(-forward));
        var chest = built.GetComponentInChildren<Container>();
        var zdo = Scene.View(chest).GetZDO();
        zdo.Set(ZDOVars.s_creator, _player.GetPlayerID());
        CreatorOf(built.GetComponent<Piece>()) = _player.GetPlayerID();
        zdo.Set(StoringPaused, true);
        return chest;
    }

    private static void Fill(Container chest, params (string Name, int Count)[] stacks)
    {
        var inventory = chest.GetInventory();
        inventory.RemoveAll();
        int slot = 0;
        foreach (var (name, count) in stacks)
        {
            inventory.AddItem(Make(PrefabOf(name), count), new Vector2i(slot % inventory.GetWidth(), slot / inventory.GetWidth()));
            slot++;
        }
    }

    private static void TakeDown(Container chest)
    {
        var view = Scene.View(chest);
        if (!view || !view.IsValid())
        {
            return;
        }

        view.ClaimOwnership();
        ZNetScene.instance.Destroy(view.gameObject);
    }
}
