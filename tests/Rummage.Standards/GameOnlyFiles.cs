using System.Collections.Generic;

namespace Rummage.Standards
{
    /// <summary>
    /// The files under src/ the unit tests leave out, as tests/Rummage.Tests/Rummage.Tests.csproj
    /// compiles src/Core alone, each with why it needs the game. The self-test reaches these in
    /// game.
    /// </summary>
    internal static class GameOnlyFiles
    {
        public static readonly Dictionary<string, string> List = new Dictionary<string, string>
        {
            ["src/Plugin.cs"] = "the plugin class, which BepInEx starts with the game",
            ["src/Settings.cs"] = "the settings, bound in BepInEx's config file with Unity's key codes",
            ["src/UnityNull.cs"] = "Unity's own check for an object that is gone, which needs UnityEngine",
            ["src/Game/**"] = "the game side: chests, inventory, wards and the other mods read through Valheim, Unity and Harmony, and the search box and readout drawn on screen; it hands src/Core what it reads",
        };
    }
}
