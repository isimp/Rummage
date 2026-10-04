using System.Collections.Generic;
using Rummage.Core;
using TMPro;
using UnityEngine;

namespace Rummage.Game;

/// <summary>
/// Two lines under the piece description in the game's build panel, for the piece the panel
/// shows (the one under the cursor in the build menu, else the one being placed): what the
/// inventory holds of each part against the next press, and what that press would bring.
/// Looked at twice a second and right after a press; worked out again only when something it
/// depends on changed (the piece, the inventory, the chests' contents or holders, the settings),
/// as the plan a press would make, so it never says more than the key would do. The text is a
/// copy of the game's own description text, so it has the game's font and look.
/// </summary>
internal sealed class BuildReadout
{
    private const float Every = 0.5f;

    private float _next;
    private int _state;
    private TMP_Text _line;

    /// <summary>How often the lines were worked out again, and how often a look found nothing changed, for the self-test.</summary>
    internal (int Worked, int Skipped) Counts { get; private set; }

    /// <summary>Works the lines out again on the next tick, as after a press.</summary>
    public void Refresh()
    {
        _next = 0f;
        _state = 0;
    }

    public void Tick(Player player, float now, string key, bool shown)
    {
        if (now < _next)
        {
            return;
        }

        _next = now + Every;
        var piece = shown && player && !player.IsDead() && CraftyLink.Ready && player.InPlaceMode() ? Context.PieceInView(player) : null;
        if (!piece)
        {
            Hide();
            return;
        }

        var nearby = CraftyLink.NearbyChests(player);
        int state = State(piece, nearby, player, key);
        if (state == _state && _line && _line.gameObject.activeSelf)
        {
            Counts = (Counts.Worked, Counts.Skipped + 1);
            return;
        }

        _state = state;
        Counts = (Counts.Worked + 1, Counts.Skipped);
        var scene = Scene.Read(player, nearby);
        var target = Context.PieceTarget(player, scene);
        if (target == null || target.Cost.Count == 0)
        {
            Hide();
            return;
        }

        var plan = Planner.Make(target.Wants(), scene.Sources, scene.Bag, CraftyLink.LeaveOne);
        string text = Readout.Panel(plan, target.Count, key, Game.Text.Shown);
        if (Line() is TMP_Text line)
        {
            if (line.text != text)
            {
                line.text = text;
            }

            line.gameObject.SetActive(true);
        }
    }

    public void Remove()
    {
        if (_line)
        {
            Object.Destroy(_line.gameObject);
        }
    }

    private void Hide()
    {
        if (_line && _line.gameObject.activeSelf)
        {
            _line.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// A fingerprint of everything the lines depend on: the piece, every stack in the inventory,
    /// each chest in reach with its data revision (which moves whenever its contents are saved)
    /// and its holder, and the settings that change a plan.
    /// </summary>
    private static int State(Piece piece, List<Container> nearby, Player player, string key)
    {
        unchecked
        {
            int state = piece.GetInstanceID();
            foreach (var item in player.GetInventory().GetAllItems())
            {
                state = state * 31 + (item.m_shared?.m_name?.GetHashCode() ?? 0);
                state = state * 31 + item.m_stack;
                state = state * 31 + item.m_gridPos.x * 64 + item.m_gridPos.y;
            }

            foreach (var chest in nearby)
            {
                var zdo = Scene.View(chest) ? Scene.View(chest).GetZDO() : null;
                state = state * 31 + chest.GetInstanceID();
                state = state * 31 + (int)(zdo?.DataRevision ?? 0);
                state = state * 31 + (zdo?.GetOwner().GetHashCode() ?? 0);
            }

            state = state * 31 + (CraftyLink.LeaveOne ? 1 : 0);
            state = state * 31 + global::Game.m_worldLevel;
            return state * 31 + key.GetHashCode();
        }
    }

    /// <summary>
    /// The copy of the description text, made once: a child of the description, hung below its
    /// box, word-wrapped to its width. Anything on the copy that would rewrite its text (the
    /// game's Localize component) is taken off.
    /// </summary>
    private TMP_Text Line()
    {
        if (_line)
        {
            return _line;
        }

        var description = Hud.instance ? Hud.instance.m_pieceDescription : null;
        if (!description)
        {
            return null;
        }

        var copy = Object.Instantiate(description.gameObject, description.transform);
        copy.name = "Rummage";
        foreach (var component in copy.GetComponents<Component>())
        {
            if (component && component.GetType().Name == "Localize")
            {
                Object.Destroy(component);
            }
        }

        foreach (Transform child in copy.transform)
        {
            Object.Destroy(child.gameObject);
        }

        var box = (RectTransform)copy.transform;
        box.anchorMin = new Vector2(0f, 0f);
        box.anchorMax = new Vector2(1f, 0f);
        box.pivot = new Vector2(0.5f, 1f);
        box.anchoredPosition = new Vector2(0f, -4f);
        box.sizeDelta = new Vector2(0f, 60f);

        _line = copy.GetComponent<TMP_Text>();
        _line.richText = true;
        _line.enableWordWrapping = true;
        _line.overflowMode = TextOverflowModes.Overflow;
        _line.fontSize = description.fontSize * 0.95f;
        return _line;
    }
}
