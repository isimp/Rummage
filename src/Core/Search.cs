using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Rummage.Core;

/// <summary>What the search box can offer.</summary>
internal enum EntryKind
{
    /// <summary>An item the chests hold; a grab brings the count asked for.</summary>
    Item,

    /// <summary>A build piece; a grab brings what that many of it cost.</summary>
    Piece,

    /// <summary>A crafting recipe; a grab brings what that many crafts cost.</summary>
    Recipe,
}

/// <summary>One line the search box can show.</summary>
internal sealed class Entry
{
    public Entry(string key, string shown, EntryKind kind, int inChests, int held = -1)
    {
        Key = key;
        Shown = shown ?? key;
        Kind = kind;
        InChests = inChests;
        Held = Math.Max(inChests, held);
    }

    /// <summary>The game's name for it: an item's shared name, a piece's or recipe's own name.</summary>
    public string Key { get; }

    /// <summary>The name the player reads.</summary>
    public string Shown { get; }

    public EntryKind Kind { get; }

    /// <summary>For an item, how many the chests can give.</summary>
    public int InChests { get; }

    /// <summary>
    /// For an item, how many the chests hold, also those kept back by Leave One Item or by chest
    /// rules and those too far away to take but near enough to find.
    /// </summary>
    public int Held { get; }
}

/// <summary>The search text split into a count and what to look for: "40 wood" and "wood 40" alike.</summary>
internal readonly struct Query : IEquatable<Query>
{
    private Query(int? count, string text)
    {
        Count = count;
        Text = text;
    }

    public int? Count { get; }

    public string Text { get; }

    public static Query Parse(string text) => Parse(text, null);

    /// <summary>
    /// The text read against the names it may stand for: when its words are the first words of a
    /// name, whole words and numbers included, it is that name and has no count; otherwise a
    /// number at either end is the count, as in "40 wood" and "wood 40".
    /// </summary>
    public static Query Parse(string text, IEnumerable<string> names)
    {
        var words = Words(text);
        if (names != null && words.Count > 0 && words.Any(IsNumber) && names.Any(name => StartsWith(name, words)))
        {
            return new Query(null, string.Join(" ", words));
        }

        int? count = null;
        if (words.Count > 0 && TryCount(words[0], out int first))
        {
            count = first;
            words.RemoveAt(0);
        }
        else if (words.Count > 1 && TryCount(words[words.Count - 1], out int last))
        {
            count = last;
            words.RemoveAt(words.Count - 1);
        }

        return new Query(count > 0 ? count : null, string.Join(" ", words));
    }

    /// <summary>Tab: the chosen name in place of what was typed, with a typed count written in front.</summary>
    public static string Complete(string text, string chosen, IEnumerable<string> names)
    {
        var query = Parse(text, names);
        return query.Count is int count ? $"{count.ToString(CultureInfo.InvariantCulture)} {chosen}" : chosen;
    }

    private static List<string> Words(string text)
        => (text ?? string.Empty).Split((char[])null, StringSplitOptions.RemoveEmptyEntries).ToList();

    private static bool IsNumber(string word) => TryCount(word, out _);

    /// <summary>Whether the name begins with these words, each whole.</summary>
    private static bool StartsWith(string name, List<string> words)
    {
        var nameWords = Words(name);
        if (words.Count > nameWords.Count)
        {
            return false;
        }

        for (int i = 0; i < words.Count; i++)
        {
            if (!string.Equals(words[i], nameWords[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryCount(string word, out int count)
        => int.TryParse(word, NumberStyles.None, CultureInfo.InvariantCulture, out count);

    public bool Equals(Query other) => Count == other.Count && string.Equals(Text, other.Text, StringComparison.Ordinal);

    public override bool Equals(object obj) => obj is Query other && Equals(other);

    public override int GetHashCode() => (Count ?? 0) * 31 + StringComparer.Ordinal.GetHashCode(Text ?? string.Empty);

    public static bool operator ==(Query left, Query right) => left.Equals(right);

    public static bool operator !=(Query left, Query right) => !left.Equals(right);
}

/// <summary>Orders what the search box offers for the text typed.</summary>
internal static class Search
{
    /// <summary>
    /// The whole name first, then names starting with the text, then a word in the name starting
    /// with it, then the text anywhere in the name, then its letters in order. Among equal matches
    /// the shorter name comes first, as it is closer to what was typed, then what the chests hold
    /// most of. With nothing typed, everything the chests hold by name, also what cannot be taken.
    /// </summary>
    public static IReadOnlyList<Entry> Rank(string text, IEnumerable<Entry> entries)
    {
        text = (text ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return entries.Where(e => e.Kind == EntryKind.Item && e.Held > 0)
                .OrderBy(e => e.Shown, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return entries.Select(e => (Entry: e, Score: Score(text, e.Shown)))
            .Where(s => s.Score >= 0)
            .OrderBy(s => s.Score)
            .ThenBy(s => s.Entry.Shown.Length)
            .ThenByDescending(s => s.Entry.InChests)
            .ThenBy(s => s.Entry.Shown, StringComparer.OrdinalIgnoreCase)
            .Select(s => s.Entry)
            .ToList();
    }

    private static int Score(string text, string name)
    {
        if (string.Equals(name, text, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (name.StartsWith(text, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        int at = name.IndexOf(text, StringComparison.OrdinalIgnoreCase);
        if (at > 0)
        {
            return name[at - 1] == ' ' || WordStartsLater(name, text, at) ? 2 : 3;
        }

        return InOrder(text, name) ? 4 : -1;
    }

    /// <summary>Whether a later occurrence of the text begins a word.</summary>
    private static bool WordStartsLater(string name, string text, int first)
    {
        for (int at = name.IndexOf(text, first + 1, StringComparison.OrdinalIgnoreCase); at > 0;
             at = name.IndexOf(text, at + 1, StringComparison.OrdinalIgnoreCase))
        {
            if (name[at - 1] == ' ')
            {
                return true;
            }
        }

        return false;
    }

    private static bool InOrder(string text, string name)
    {
        int next = 0;
        foreach (char c in text)
        {
            if (c == ' ')
            {
                continue;
            }

            next = name.IndexOf(c.ToString(), next, StringComparison.OrdinalIgnoreCase);
            if (next < 0)
            {
                return false;
            }

            next++;
        }

        return true;
    }
}
