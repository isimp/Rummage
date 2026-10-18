using System;
using System.IO;
using Rummage.Core;

namespace Rummage.Game;

/// <summary>
/// What the search box last found or took (<see cref="Recent"/>), for the character being played,
/// kept in a small file per character under BepInEx's cache folder. The config folder is not
/// used, as a mod manager's profile sync replaces what is there. A file that cannot be read or
/// written is told once in the log, and the search box goes on with what it holds in memory.
/// With RememberLastUsed off the search box sees nothing remembered and nothing new is remembered;
/// the file stays as it was.
/// </summary>
internal static class LastUsed
{
    /// <summary>What the search box sees with RememberLastUsed off; never changed.</summary>
    private static readonly Recent Off = new();

    private static Recent _recent = new();
    private static long _player = long.MinValue;
    private static int _saved;
    private static bool _told;

    /// <summary>The folder the files are in, one per character, named by its player id.</summary>
    internal static string Folder => Path.Combine(BepInEx.Paths.CachePath, Plugin.Guid, "last-used");

    /// <summary>The character being played's, read from its file the first time; nothing while RememberLastUsed is off.</summary>
    public static Recent Current => Settings.RememberLastUsed.Value ? Loaded : Off;

    private static Recent Loaded
    {
        get
        {
            long player = PlayerId();
            if (player != _player)
            {
                _player = player;
                _recent = player == 0 ? new Recent() : Read(FileOf(player));
                _saved = _recent.Revision;
            }

            return _recent;
        }
    }

    public static void Found(EntryKind kind, string key)
    {
        if (Settings.RememberLastUsed.Value)
        {
            Loaded.Found(kind, key);
            Save();
        }
    }

    public static void Took(EntryKind kind, string key)
    {
        if (Settings.RememberLastUsed.Value)
        {
            Loaded.Took(kind, key);
            Save();
        }
    }

    /// <summary>Forgets what was last used, in memory and on disk, for the character being played, whether or not RememberLastUsed is on.</summary>
    public static void Forget()
    {
        Loaded.Forget();
        _saved = _recent.Revision;
        if (_player == 0)
        {
            return;
        }

        try
        {
            // File.Delete throws when the folder is missing, which it is until the first save.
            string path = FileOf(_player);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception e)
        {
            WarnOnce("delete", e);
        }
    }

    /// <summary>The path of the file for the character being played, for the self-test.</summary>
    internal static string FileOfCurrent => _player == 0 ? null : FileOf(_player);

    /// <summary>Reads the character's file again, as at the next game, for the self-test.</summary>
    internal static void ReadAgain() => _player = long.MinValue;

    /// <summary>Puts back what <see cref="Recent.Save"/> wrote, in memory and on disk, as the self-test does with the player's own.</summary>
    internal static void Replace(string saved)
    {
        _ = Loaded;
        if (Recent.Load(saved).Entries.Count == 0)
        {
            Forget();
            return;
        }

        _recent = Recent.Load(saved);
        _saved = _recent.Revision - 1;
        Save();
    }

    private static void Save()
    {
        if (_player == 0 || _recent.Revision == _saved)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Folder);
            string path = FileOf(_player);
            string temp = path + ".tmp";
            // Written beside the file first, so a game that ends meanwhile leaves the old file whole.
            File.WriteAllText(temp, _recent.Save());
            File.Copy(temp, path, overwrite: true);
            File.Delete(temp);

            _saved = _recent.Revision;
        }
        catch (Exception e)
        {
            WarnOnce("write", e);
        }
    }

    private static Recent Read(string path)
    {
        try
        {
            return File.Exists(path) ? Recent.Load(File.ReadAllText(path)) : new Recent();
        }
        catch (Exception e)
        {
            WarnOnce("read", e);
            return new Recent();
        }
    }

    private static string FileOf(long player) => Path.Combine(Folder, player.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".txt");

    private static long PlayerId() => global::Game.instance != null && global::Game.instance.GetPlayerProfile() != null ? global::Game.instance.GetPlayerProfile().GetPlayerID() : 0;

    private static void WarnOnce(string what, Exception e)
    {
        if (!_told)
        {
            _told = true;
            Plugin.Log.LogWarning($"Could not {what} the search box's last used file in {Folder}: {e.GetType().Name}: {e.Message}");
        }
    }
}
