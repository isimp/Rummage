using Rummage.Core;
using UnityEngine;

namespace Rummage.Game;

/// <summary>
/// While a build tool is out or the build menu is open: one line for the selected or hovered
/// piece, telling what the inventory holds of each part against what the next press is for, and
/// what that press would bring. Worked out twice a second and right after a press, as the plan a
/// press would make, so it never says more than the key would do.
/// </summary>
internal sealed class BuildReadout
{
    private const float Every = 0.5f;

    private float _next;
    private string _line;

    /// <summary>Works the line out again on the next tick, as after a press.</summary>
    public void Refresh() => _next = 0f;

    public void Tick(Player player, float now, string key)
    {
        if (now < _next)
        {
            return;
        }

        _next = now + Every;
        _line = null;
        if (!player || player.IsDead() || Palette.IsOpen || !CraftyLink.Ready || Menu.IsVisible() || InventoryGui.IsVisible()
            || !(player.InPlaceMode() || Hud.IsPieceSelectionVisible()))
        {
            return;
        }

        var target = Context.PieceTarget(player);
        if (target == null)
        {
            return;
        }

        var scene = Scene.Read(player);
        var plan = Planner.Make(target.Wants(), scene.Sources, scene.Bag, CraftyLink.LeaveOne);
        _line = Readout.Line(target.Label, plan, key, Text.Shown);
    }

    public void OnGUI(float height)
    {
        if (_line == null || Palette.IsOpen || Event.current.type != EventType.Repaint)
        {
            return;
        }

        Skin.Build();
        float scale = Mathf.Max(0.5f, Screen.height / 1080f);
        var matrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

        var content = new GUIContent(_line);
        var size = Skin.Line.CalcSize(content);
        float width = Mathf.Min(size.x + 24f, Screen.width / scale - 32f);
        var area = new Rect((Screen.width / scale - width) / 2f, Screen.height / scale * height, width, size.y + 10f);
        GUI.Box(area, GUIContent.none, Skin.Box);
        GUI.Label(new Rect(area.x + 12f, area.y + 5f, area.width - 24f, size.y), content, Skin.Line);

        GUI.matrix = matrix;
    }
}
