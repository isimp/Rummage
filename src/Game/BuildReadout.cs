using Rummage.Core;
using TMPro;
using UnityEngine;

namespace Rummage.Game;

/// <summary>
/// Two lines under the piece description in the game's build panel, for the piece the panel
/// shows (the one under the cursor in the build menu, else the one being placed): what the
/// inventory holds of each part against the next press, and what that press would bring.
/// Worked out twice a second and right after a press, as the plan a press would make, so it
/// never says more than the key would do. The text is a copy of the game's own description text,
/// so it has the game's font and look.
/// </summary>
internal sealed class BuildReadout
{
    private const float Every = 0.5f;

    private float _next;
    private TMP_Text _line;

    /// <summary>Works the lines out again on the next tick, as after a press.</summary>
    public void Refresh() => _next = 0f;

    public void Tick(Player player, float now, string key, bool shown)
    {
        if (now < _next)
        {
            return;
        }

        _next = now + Every;
        string text = shown ? Text(player, key) : null;
        if (text == null)
        {
            if (_line)
            {
                _line.gameObject.SetActive(false);
            }

            return;
        }

        if (Line() is TMP_Text line)
        {
            line.text = text;
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

    private static string Text(Player player, string key)
    {
        if (!player || player.IsDead() || !CraftyLink.Ready || !player.InPlaceMode())
        {
            return null;
        }

        var target = Context.PieceTarget(player);
        if (target == null || target.Cost.Count == 0)
        {
            return null;
        }

        var scene = Scene.Read(player);
        var plan = Planner.Make(target.Wants(), scene.Sources, scene.Bag, CraftyLink.LeaveOne);
        return Readout.Panel(plan, target.Count, key, Game.Text.Shown);
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
