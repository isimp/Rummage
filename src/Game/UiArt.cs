using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;

namespace Rummage.Game;

/// <summary>
/// The game's own UI art for the search box: the sprites of its UIAtlas (wood panel, buttons,
/// text field), drawn as nine-part frames, and item icons. When the atlas or a sprite cannot be
/// had, the caller falls back to plain fills.
/// </summary>
internal static class UiArt
{
    public static readonly Color Orange = new(1f, 0.631f, 0.235f, 1f);
    public static readonly Color Beige = new(0.853f, 0.725f, 0.533f, 1f);

    private static readonly Dictionary<string, Sprite> Sprites = new();
    private static Dictionary<string, Sprite> _itemIcons;
    private static SpriteAtlas _atlas;
    private static bool _looked;

    public static Sprite Get(string name)
    {
        if (Sprites.TryGetValue(name, out var cached))
        {
            return cached;
        }

        if (!_looked || !_atlas)
        {
            _looked = true;
            foreach (var atlas in Resources.FindObjectsOfTypeAll<SpriteAtlas>())
            {
                if (atlas && atlas.name == "UIAtlas")
                {
                    _atlas = atlas;
                    break;
                }
            }
        }

        var sprite = _atlas ? _atlas.GetSprite(name) : null;
        if (sprite)
        {
            Sprites[name] = sprite;
        }

        return sprite;
    }

    /// <summary>Draws a sprite stretched to a box, its borders kept at their size; false when it cannot.</summary>
    public static bool Sliced(Rect box, string name, Color tint)
    {
        var sprite = Get(name);
        if (!sprite || !TryArea(sprite, out var area))
        {
            return false;
        }

        var texture = sprite.texture;
        var border = sprite.border;
        float fit = Mathf.Min(1f, box.width / Mathf.Max(1f, border.x + border.z), box.height / Mathf.Max(1f, border.y + border.w));
        float left = border.x * fit;
        float right = border.z * fit;
        float top = border.w * fit;
        float bottom = border.y * fit;

        float[] xs = [box.x, box.x + left, box.xMax - right, box.xMax];
        float[] ys = [box.y, box.y + top, box.yMax - bottom, box.yMax];
        float[] us = [area.x, area.x + border.x, area.xMax - border.z, area.xMax];
        float[] vs = [area.yMax, area.yMax - border.w, area.y + border.y, area.y];

        var color = GUI.color;
        GUI.color = tint;
        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                var piece = Rect.MinMaxRect(xs[column], ys[row], xs[column + 1], ys[row + 1]);
                if (piece.width <= 0f || piece.height <= 0f)
                {
                    continue;
                }

                var uv = Rect.MinMaxRect(us[column] / texture.width, vs[row + 1] / texture.height, us[column + 1] / texture.width, vs[row] / texture.height);
                GUI.DrawTextureWithTexCoords(piece, texture, uv);
            }
        }

        GUI.color = color;
        return true;
    }

    /// <summary>An item's icon by its shared name, from the game's item list.</summary>
    public static Sprite ItemIcon(string name)
    {
        if (_itemIcons == null && ObjectDB.instance)
        {
            _itemIcons = new Dictionary<string, Sprite>();
            foreach (var prefab in ObjectDB.instance.m_items)
            {
                var item = prefab && prefab.TryGetComponent(out ItemDrop drop) ? drop.m_itemData : null;
                if (item?.m_shared != null && item.m_shared.m_icons != null && item.m_shared.m_icons.Length > 0 && !_itemIcons.ContainsKey(item.m_shared.m_name))
                {
                    _itemIcons[item.m_shared.m_name] = item.GetIcon();
                }
            }
        }

        return _itemIcons != null && _itemIcons.TryGetValue(name, out var icon) ? icon : null;
    }

    /// <summary>Draws a sprite whole in a box, such as an item's icon.</summary>
    public static void Icon(Rect box, Sprite sprite)
    {
        if (!sprite || !TryArea(sprite, out var area))
        {
            return;
        }

        var texture = sprite.texture;
        GUI.DrawTextureWithTexCoords(box, texture, new Rect(area.x / texture.width, area.y / texture.height, area.width / texture.width, area.height / texture.height));
    }

    /// <summary>Where a sprite sits in its texture; a sprite packed tight has no such box and is not drawn.</summary>
    private static bool TryArea(Sprite sprite, out Rect area)
    {
        try
        {
            area = sprite.textureRect;
            return sprite.texture;
        }
        catch (UnityException)
        {
            area = default;
            return false;
        }
    }
}
