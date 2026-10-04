using UnityEngine;

namespace Rummage.Game;

/// <summary>
/// Draws the search box. Unity runs its GUI pass every frame for every enabled component that
/// draws, so this one is switched on only while the search box is open, and once early in a
/// world to warm the fonts.
/// </summary>
internal sealed class PaletteView : MonoBehaviour
{
    internal Palette Palette { get; set; }

    private void OnGUI() => Palette?.OnGUI();
}