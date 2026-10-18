using System;
using System.Globalization;
using System.IO;

namespace Rummage.Core;

/// <summary>
/// The file per character that keeps what the search box last found or took (<see cref="Recent"/>),
/// named by its player id in one folder. A file is written beside the old one first and only then
/// put in its place, so a write that fails, or a game that ends meanwhile, leaves the old one whole.
/// A file that cannot be read or written is told once, however often it happens; reading it then
/// gives nothing remembered.
/// </summary>
internal sealed class RecentFile
{
    private readonly string _folder;
    private readonly Action<string> _warn;
    private bool _told;

    /// <param name="folder">The folder the files are in.</param>
    /// <param name="warn">Where a failure is told.</param>
    public RecentFile(string folder, Action<string> warn)
    {
        _folder = folder;
        _warn = warn;
    }

    /// <summary>The character's file.</summary>
    public string PathOf(long player) => Path.Combine(_folder, player.ToString(CultureInfo.InvariantCulture) + ".txt");

    /// <summary>What the character's file holds; nothing remembered for a character with no file yet, or one that cannot be read.</summary>
    public Recent Read(long player)
    {
        try
        {
            string path = PathOf(player);
            return File.Exists(path) ? Recent.Load(File.ReadAllText(path)) : new Recent();
        }
        catch (Exception e)
        {
            WarnOnce("read", e);
            return new Recent();
        }
    }

    /// <summary>Writes the character's file; false, and told, when it cannot be written.</summary>
    public bool Write(long player, Recent recent)
    {
        try
        {
            Directory.CreateDirectory(_folder);
            string path = PathOf(player);
            string temp = path + ".tmp";
            File.WriteAllText(temp, recent.Save());
            File.Copy(temp, path, overwrite: true);
            File.Delete(temp);
            return true;
        }
        catch (Exception e)
        {
            WarnOnce("write", e);
            return false;
        }
    }

    /// <summary>Deletes the character's file, if it has one.</summary>
    public void Delete(long player)
    {
        try
        {
            // File.Delete throws when the folder is missing, which it is until the first save.
            string path = PathOf(player);
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

    private void WarnOnce(string what, Exception e)
    {
        if (!_told)
        {
            _told = true;
            _warn($"Could not {what} the search box's last used file in {_folder}: {e.GetType().Name}: {e.Message}");
        }
    }
}
