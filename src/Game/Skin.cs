using System;
using System.Text;
using UnityEngine;

namespace Rummage.Game;

/// <summary>
/// The search box's look: colours, flat textures and text styles, all made in code, in the
/// game's own Averia Serif font once it is loaded.
/// </summary>
internal static class Skin
{
    public static readonly Color Back = new(0.066f, 0.071f, 0.086f, 0.96f);
    public static readonly Color Sunken = new(0.045f, 0.050f, 0.062f, 1f);
    public static readonly Color Selected = new(0.960f, 0.720f, 0.340f, 0.18f);
    public static readonly Color Hover = new(1f, 1f, 1f, 0.05f);
    public static readonly Color Text = new(0.930f, 0.910f, 0.870f, 1f);
    public static readonly Color Faint = new(0.600f, 0.605f, 0.640f, 1f);
    public static readonly Color Accent = new(0.960f, 0.720f, 0.340f, 1f);

    private static readonly string Sample = Letters();
    private static bool _built;
    private static bool _warmed;

    public static GUIStyle Box { get; private set; }

    public static GUIStyle Field { get; private set; }

    public static GUIStyle Name { get; private set; }

    public static GUIStyle Detail { get; private set; }

    public static GUIStyle Hint { get; private set; }

    public static GUIStyle Button { get; private set; }

    public static Texture2D SelectedFill { get; private set; }

    public static Texture2D HoverFill { get; private set; }

    /// <summary>
    /// Builds the styles and lays out every letter once. The first text drawn in a session costs
    /// the better part of a second while Unity loads its GUI skin and the font's glyphs; done in
    /// a world before the search box first opens, that cost never lands on a key press.
    /// </summary>
    public static void Warm()
    {
        if (_warmed || !Player.m_localPlayer)
        {
            return;
        }

        Build();
        var sample = new GUIContent(Sample);
        foreach (var style in new[] { Field, Name, Detail, Hint, Button })
        {
            style.CalcSize(sample);
        }

        _warmed = true;
    }

    public static void Build()
    {
        if (_built)
        {
            return;
        }

        var font = GameFont();
        Box = new GUIStyle(GUI.skin.box) { normal = { background = Fill(Back) }, border = new RectOffset(0, 0, 0, 0) };
        Field = new GUIStyle(GUI.skin.textField)
        {
            font = font,
            fontSize = 20,
            padding = new RectOffset(10, 10, 6, 6),
            normal = { background = Fill(Sunken), textColor = Text },
            focused = { background = Fill(Sunken), textColor = Text },
            hover = { background = Fill(Sunken), textColor = Text },
        };
        Name = Label(font, 18, Text, TextAnchor.MiddleLeft);
        Detail = Label(font, 15, Faint, TextAnchor.MiddleRight);
        Hint = Label(font, 14, Faint, TextAnchor.MiddleLeft);
        Button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 17 };
        SelectedFill = Fill(Selected);
        HoverFill = Fill(Hover);
        _built = true;
    }

    private static GUIStyle Label(Font font, int size, Color color, TextAnchor anchor)
        => new(GUI.skin.label) { font = font, fontSize = size, alignment = anchor, normal = { textColor = color }, wordWrap = false, clipping = TextClipping.Clip };

    private static Font GameFont()
    {
        foreach (var font in Resources.FindObjectsOfTypeAll<Font>())
        {
            if (font && font.name.Equals("AveriaSerifLibre-Regular", StringComparison.OrdinalIgnoreCase))
            {
                return font;
            }
        }

        return null;
    }

    private static Texture2D Fill(Color color)
    {
        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    private static string Letters()
    {
        var every = new StringBuilder();
        for (char c = ' '; c <= '~'; c++)
        {
            every.Append(c);
        }

        return every.Append("·×↑↓").ToString();
    }
}
