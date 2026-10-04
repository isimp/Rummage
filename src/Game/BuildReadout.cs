using System.Collections.Generic;
using Rummage.Core;
using TMPro;
using UnityEngine;

namespace Rummage.Game;

/// <summary>
/// Two lines beside the game's build panel, for the piece the panel
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
        if (state == _state && _line && _line.transform.parent.gameObject.activeSelf)
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

            line.transform.parent.gameObject.SetActive(true);
            Place(line);
        }
    }

    public void Remove()
    {
        if (_line)
        {
            Object.Destroy(_line.transform.parent.gameObject);
        }
    }

    private void Hide()
    {
        if (_line && _line.transform.parent.gameObject.activeSelf)
        {
            _line.transform.parent.gameObject.SetActive(false);
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

    private static readonly Vector3[] Corners = new Vector3[4];

    /// <summary>
    /// The lines, made once: a dark box beside the build panel's content holding a copy of the
    /// description's own text, so it has the game's font. Anything on the copy that would rewrite
    /// its text (the game's Localize component) is taken off.
    /// </summary>
    private TMP_Text Line()
    {
        if (_line)
        {
            return _line;
        }

        var hud = Hud.instance;
        var description = hud ? hud.m_pieceDescription : null;
        if (!description || !hud.m_buildHud)
        {
            return null;
        }

        var box = new GameObject("Rummage", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        box.transform.SetParent(hud.m_buildHud.transform, false);
        box.GetComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.62f);
        ((RectTransform)box.transform).pivot = new Vector2(0f, 0f);

        var copy = Object.Instantiate(description.gameObject, box.transform);
        copy.name = "Text";
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

        var text = (RectTransform)copy.transform;
        text.anchorMin = Vector2.zero;
        text.anchorMax = Vector2.one;
        text.pivot = new Vector2(0f, 1f);
        text.offsetMin = new Vector2(8f, 4f);
        text.offsetMax = new Vector2(-8f, -4f);

        _line = copy.GetComponent<TMP_Text>();
        _line.richText = true;
        _line.enableWordWrapping = true;
        _line.overflowMode = TextOverflowModes.Overflow;
        _line.fontSize = description.fontSize * 0.9f;
        return _line;
    }

    /// <summary>The widest the box gets, so it never reaches over the chat or other parts of the screen.</summary>
    private const float MaxWidth = 520f;

    /// <summary>
    /// Puts the box to the right of the whole build panel (the piece's name, description, icon and
    /// cost row), top-aligned with it and as tall as it. It is as wide as its longest line up to
    /// <see cref="MaxWidth"/>; only a line longer than that wraps, and then the box grows taller.
    /// </summary>
    private static void Place(TMP_Text line)
    {
        var hud = Hud.instance;
        var box = (RectTransform)line.transform.parent;
        var parent = (RectTransform)box.parent;
        float left = float.MaxValue;
        float right = float.MinValue;
        float top = float.MinValue;
        float bottom = float.MaxValue;
        Grow(hud.m_buildSelection ? hud.m_buildSelection.rectTransform : null, ref left, ref right, ref top, ref bottom);
        Grow(hud.m_pieceDescription.rectTransform, ref left, ref right, ref top, ref bottom);
        Grow(hud.m_buildIcon ? hud.m_buildIcon.rectTransform : null, ref left, ref right, ref top, ref bottom);
        foreach (var item in hud.m_requirementItems)
        {
            if (item && item.activeInHierarchy)
            {
                foreach (var part in item.GetComponentsInChildren<RectTransform>())
                {
                    Grow(part, ref left, ref right, ref top, ref bottom);
                }
            }
        }

        var topRight = parent.InverseTransformPoint(new Vector3(right, top, 0f));
        var bottomRight = parent.InverseTransformPoint(new Vector3(right, bottom, 0f));
        float width = Mathf.Min(line.GetPreferredValues(line.text).x, MaxWidth) + 16f;
        float height = Mathf.Max(topRight.y - bottomRight.y, line.GetPreferredValues(line.text, width - 16f, 0f).y + 8f);
        box.anchorMin = box.anchorMax = new Vector2(0.5f, 0.5f);
        box.pivot = new Vector2(0f, 1f);
        box.sizeDelta = new Vector2(width, height);
        box.localPosition = new Vector3(topRight.x + 14f, topRight.y, 0f);
    }
    private static void Grow(RectTransform part, ref float left, ref float right, ref float top, ref float bottom)
    {
        if (!part || !part.gameObject.activeInHierarchy)
        {
            return;
        }

        part.GetWorldCorners(Corners);
        left = Mathf.Min(left, Corners[0].x);
        right = Mathf.Max(right, Corners[2].x);
        top = Mathf.Max(top, Corners[2].y);
        bottom = Mathf.Min(bottom, Corners[0].y);
    }
}