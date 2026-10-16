using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Rummage.Core;

/// <summary>What Enter does in the search box.</summary>
internal enum EnterDoes
{
    /// <summary>Lights up the chests that hold it, taking nothing.</summary>
    Find,

    /// <summary>Takes the count typed or set on the slider.</summary>
    Take,

    /// <summary>Takes one: one item, or what a press of the key would take for a piece or recipe.</summary>
    TakeOne,
}

/// <summary>Something the search box found or took.</summary>
internal sealed class Used
{
    public Used(EntryKind kind, string key, bool taken)
    {
        Kind = kind;
        Key = key;
        Taken = taken;
    }

    public EntryKind Kind { get; }

    /// <summary>The entry's key, as in <see cref="Entry.Key"/>.</summary>
    public string Key { get; }

    /// <summary>Whether it was last taken rather than found.</summary>
    public bool Taken { get; }

    public bool Is(EntryKind kind, string key) => Kind == kind && string.Equals(Key, key, StringComparison.Ordinal);
}

/// <summary>
/// The last things the search box found or took, newest first, which it lists first with nothing
/// typed. Something found stays found until it is taken or something else is found, and Enter on
/// it then takes one rather than finding it again. Kept per character between games through
/// <see cref="Save"/> and <see cref="Load"/>.
/// </summary>
internal sealed class Recent
{
    /// <summary>How many are kept.</summary>
    public const int Kept = 2;

    private readonly List<Used> _entries = new();
    private Used _found;

    /// <summary>Newest first.</summary>
    public IReadOnlyList<Used> Entries => _entries;

    /// <summary>Moves on with every change, so a caller can tell when to save.</summary>
    public int Revision { get; private set; }

    public void Found(EntryKind kind, string key)
    {
        if (Remember(kind, key, taken: false) is Used used)
        {
            _found = used;
        }
    }

    public void Took(EntryKind kind, string key)
    {
        if (Remember(kind, key, taken: true) is not null && _found != null && _found.Is(kind, key))
        {
            _found = null;
        }
    }

    public void Forget()
    {
        _entries.Clear();
        _found = null;
        Revision++;
    }

    public bool IsFound(EntryKind kind, string key) => _found != null && _found.Is(kind, key);

    /// <summary>
    /// The new items row and a typed count take; Shift finds; Enter on what was found takes one;
    /// anything else is found.
    /// </summary>
    public EnterDoes Enter(EntryKind kind, string key, bool countTyped, bool shift)
    {
        if (kind == EntryKind.New || countTyped)
        {
            return EnterDoes.Take;
        }

        return !shift && IsFound(kind, key) ? EnterDoes.TakeOne : EnterDoes.Find;
    }

    /// <summary>The word a row shows: "found" while Enter on it takes one, "taken" when it was last taken, else none.</summary>
    public string Tag(EntryKind kind, string key)
    {
        if (IsFound(kind, key))
        {
            return "found";
        }

        return _entries.Any(u => u.Is(kind, key) && u.Taken) ? "taken" : null;
    }

    /// <summary>One line each, newest first: kind, then "found", "taken" or "seen", then the key, split by tabs.</summary>
    public string Save()
    {
        var text = new StringBuilder();
        foreach (var used in _entries)
        {
            string state = IsFound(used.Kind, used.Key) ? "found" : used.Taken ? "taken" : "seen";
            text.Append(used.Kind).Append('\t').Append(state).Append('\t').Append(used.Key).Append('\n');
        }

        return text.ToString();
    }

    /// <summary>What <see cref="Save"/> wrote; a line that cannot be read is skipped.</summary>
    public static Recent Load(string text)
    {
        var recent = new Recent();
        var lines = (text ?? string.Empty).Split('\n');
        for (int i = lines.Length - 1; i >= 0; i--)
        {
            string[] fields = lines[i].TrimEnd('\r').Split('\t');
            if (fields.Length != 3 || fields[2].Length == 0 || !Enum.TryParse(fields[0], out EntryKind kind) || !Enum.IsDefined(typeof(EntryKind), kind))
            {
                continue;
            }

            switch (fields[1])
            {
                case "found":
                    recent.Found(kind, fields[2]);
                    break;
                case "taken":
                    recent.Took(kind, fields[2]);
                    break;
                case "seen":
                    recent.Remember(kind, fields[2], taken: false);
                    break;
            }
        }

        recent.Revision = 0;
        return recent;
    }

    /// <summary>Puts it first, once, and drops what falls past <see cref="Kept"/>. The new items row is not remembered.</summary>
    private Used Remember(EntryKind kind, string key, bool taken)
    {
        if (kind == EntryKind.New || string.IsNullOrEmpty(key))
        {
            return null;
        }

        _entries.RemoveAll(u => u.Is(kind, key));
        var used = new Used(kind, key, taken);
        _entries.Insert(0, used);
        if (_entries.Count > Kept)
        {
            _entries.RemoveRange(Kept, _entries.Count - Kept);
        }

        Revision++;
        return used;
    }
}
