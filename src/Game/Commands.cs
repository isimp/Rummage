using System;
using System.Collections.Generic;
using System.Linq;

namespace Rummage.Game;

/// <summary>
/// The /rummage chat command and what may follow it. The game keeps one handler per command name,
/// so the self-test adds its word here rather than registering /rummage again.
/// </summary>
internal static class Commands
{
    private static readonly SortedDictionary<string, (string Help, Action<Terminal.ConsoleEventArgs> Run)> Words = new(StringComparer.OrdinalIgnoreCase);

    public static void Add(string word, string help, Action<Terminal.ConsoleEventArgs> run)
    {
        Words[word] = (help, run);
        _ = new Terminal.ConsoleCommand("rummage", string.Join(" | ", Words.Select(w => $"{w.Key} - {w.Value.Help}")), Dispatch,
            optionsFetcher: () => Words.Keys.ToList());
    }

    private static void Dispatch(Terminal.ConsoleEventArgs args)
    {
        if (args.Length >= 2 && Words.TryGetValue(args[1], out var word))
        {
            word.Run(args);
            return;
        }

        args.Context.OrNull()?.AddString($"Usage: /rummage {string.Join(" | ", Words.Keys)}");
    }
}
