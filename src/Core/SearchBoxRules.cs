using System;

namespace Rummage.Core;

/// <summary>What the search box offers and says, with no game in it; the box itself is drawn by the game side.</summary>
internal static class SearchBoxRules
{
    /// <summary>Whether what the player was looking at when the box opened is listed first: while its name is typed.</summary>
    public static bool ContextFirst(string typed, string contextShown) =>
        contextShown != null && string.Equals(typed, contextShown, StringComparison.OrdinalIgnoreCase);

    /// <summary>Where the count starts for a listed entry: an item at a stack, or what the chests give if less; a piece or recipe at one.</summary>
    public static int StartCount(EntryKind kind, int stackSize, int inChests) =>
        kind == EntryKind.Item ? Math.Max(1, Math.Min(stackSize, inChests)) : 1;

    /// <summary>The slider's top: what the chests give of an item, or how many of a piece or recipe can be paid for, never below the start.</summary>
    public static int MaxCount(int start, int inChests) => Math.Max(start, Math.Max(1, inChests));

    /// <summary>The count a take is for: a number typed, else the slider's, rounded as the game rounds.</summary>
    public static int CountFor(int? typed, float slider) => typed ?? (int)Math.Round(slider);

    /// <summary>The key hints for the chosen row: what Enter does with it now.</summary>
    public static string Hint(bool newItems, bool takesOne)
    {
        if (newItems)
        {
            return "Enter takes one of each \u00b7 \u2191\u2193 choose \u00b7 Esc close";
        }

        return takesOne
            ? "Enter takes one \u00b7 Shift+Enter finds again \u00b7 \u2191\u2193 choose \u00b7 Esc close"
            : "Enter find \u00b7 a number, then Enter, takes \u00b7 Tab complete \u00b7 \u2191\u2193 choose \u00b7 Esc close";
    }
}
