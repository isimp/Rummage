using System;

namespace Rummage.Core;

/// <summary>
/// The search box's list: which row is chosen and which rows show. Arrows move the chosen row,
/// never past either end, and the rows shown follow it so it is always in view.
/// </summary>
internal sealed class ListWindow
{
    /// <summary>The chosen row.</summary>
    public int Selected { get; private set; }

    /// <summary>The first row shown.</summary>
    public int First { get; private set; }

    /// <summary>A new search starts at the top.</summary>
    public void Restart()
    {
        Selected = 0;
        First = 0;
    }

    /// <summary>The mouse wheel moves the rows shown three at a time, never past the last rows or the top; the chosen row stays.</summary>
    public void Scroll(bool down, int count, int visible)
    {
        First = Math.Max(0, Math.Min(First + (down ? 3 : -3), Math.Max(0, count - visible)));
    }

    /// <summary>A click chooses a shown row.</summary>
    public void Click(int index)
    {
        Selected = index;
    }

    /// <summary>Chooses a row of <paramref name="count"/>, <paramref name="visible"/> of which show at a time.</summary>
    public void Choose(int index, int count, int visible)
    {
        if (count == 0)
        {
            return;
        }

        Selected = Math.Max(0, Math.Min(index, count - 1));
        if (Selected < First)
        {
            First = Selected;
        }
        else if (Selected >= First + visible)
        {
            First = Selected - visible + 1;
        }
    }
}
