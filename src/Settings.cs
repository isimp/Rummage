using BepInEx.Configuration;
using Rummage.Core;
using UnityEngine;

namespace Rummage;

/// <summary>Which key opens the search box together with the Rummage key.</summary>
internal enum SearchModifier
{
    Shift,
    Control,
    None,
}

/// <summary>Everything in Rummage's config file.</summary>
internal static class Settings
{
    public static ConfigEntry<KeyCode> Key { get; private set; }

    public static ConfigEntry<SearchModifier> Modifier { get; private set; }

    public static ConfigEntry<KeyCode> SearchKey { get; private set; }

    public static ConfigEntry<bool> KeyAloneOpensSearch { get; private set; }

    public static ConfigEntry<bool> TakeForRecipes { get; private set; }

    public static ConfigEntry<bool> TakeForPieces { get; private set; }

    public static ConfigEntry<bool> TopUpStacks { get; private set; }

    public static ConfigEntry<int> TopUpAmount { get; private set; }

    public static ConfigEntry<bool> ShowItems { get; private set; }

    public static ConfigEntry<bool> ShowPieces { get; private set; }

    public static ConfigEntry<bool> ShowRecipes { get; private set; }

    public static ConfigEntry<float> FindRadius { get; private set; }

    public static ConfigEntry<int> Rows { get; private set; }

    public static ConfigEntry<float> Scale { get; private set; }

    public static ConfigEntry<BuildBox> BuildBox { get; private set; }

    public static ConfigEntry<Messages> Messages { get; private set; }

    public static ConfigEntry<bool> PlaySounds { get; private set; }

    public static void Bind(ConfigFile config)
    {
        Key = config.Bind("Keys", "Key", KeyCode.J,
            "Takes what the thing in front of you needs from the chests around you: a stack in your inventory under the cursor, "
            + "the recipe selected in the crafting tab, the build piece under the cursor in the build menu, or the piece you are placing. "
            + "Ignored while Alt is held, so Alt with the same key stays free for other mods.");
        Modifier = config.Bind("Keys", "SearchModifier", SearchModifier.Shift,
            "Held with Key, opens the search box starting from the thing in front of you, its count filled in. None turns this off.");
        SearchKey = config.Bind("Keys", "SearchKey", KeyCode.None,
            "A key of its own that opens the search box, starting from the thing in front of you when there is one. "
            + "When it is the same key as Key, it is the one that counts.");
        KeyAloneOpensSearch = config.Bind("Keys", "KeyAloneOpensSearch", true,
            "Whether Key opens the search box when there is nothing in front of you to take for. Turned off, Key does nothing there.");

        TakeForRecipes = config.Bind("Taking", "TakeForRecipes", true,
            "Whether Key takes for the recipe selected in the crafting tab.");
        TakeForPieces = config.Bind("Taking", "TakeForPieces", true,
            "Whether Key takes for build pieces. Turned off, the box beside the build panel does not show either.");
        TopUpStacks = config.Bind("Taking", "TopUpStacks", true,
            "Whether Key over a stack in your inventory fills that stack from the chests.");
        TopUpAmount = config.Bind("Taking", "TopUpAmount", 0,
            new ConfigDescription("How many one press over a stack takes at most. 0 fills the stack to a full one.",
                new AcceptableValueRange<int>(0, 1000)));

        ShowItems = config.Bind("Search", "ShowItems", true, "Whether the search box lists what the chests hold.");
        ShowPieces = config.Bind("Search", "ShowPieces", true, "Whether the search box lists the build pieces you know.");
        ShowRecipes = config.Bind("Search", "ShowRecipes", true, "Whether the search box lists the recipes you know.");
        FindRadius = config.Bind("Search", "FindRadius", 50f,
            new ConfigDescription("How far, in metres, the search box looks for chests to list what they hold and to light up when finding. "
                + "Taking stays within AzuCraftyBoxes' own range.", new AcceptableValueRange<float>(10f, 150f)));
        Rows = config.Bind("Search", "Rows", 10,
            new ConfigDescription("How many results the search box shows at once.", new AcceptableValueRange<int>(4, 20)));
        Scale = config.Bind("Search", "Scale", 1f,
            new ConfigDescription("The size of the search box, on top of the size that follows the screen's height.",
                new AcceptableValueRange<float>(0.6f, 1.6f)));

        BuildBox = config.Bind("Display", "BuildBox", Core.BuildBox.Always,
            "When the box beside the game's build panel shows what you carry of each part of the piece and what Key would take: "
            + "Always, UntilCarried (hidden while you carry enough to build the piece) or Never.");
        Messages = config.Bind("Display", "Messages", Core.Messages.Full,
            "What you read after Key takes: Full (what was taken, and why anything is short), Short (what was taken and what is short) or Off. "
            + "What finding turns up is always told.");
        PlaySounds = config.Bind("Display", "PlaySounds", true,
            "Play a short sound when the search box opens or closes, when Key takes something and when finding lights up chests. Only you hear them, at the game's own sound effects volume.");
    }

    /// <summary>
    /// The settings as they ship, for the self-test, which sets them while it runs and puts the
    /// player's back after. Nothing is written to the config file in between, so the player's
    /// values stay on disk even if the game ends before they are put back.
    /// </summary>
    internal static System.Action UseDefaults()
    {
        var file = Key.ConfigFile;
        bool saving = file.SaveOnConfigSet;
        file.SaveOnConfigSet = false;
        var entries = new ConfigEntryBase[]
        {
            Key, Modifier, SearchKey, KeyAloneOpensSearch, TakeForRecipes, TakeForPieces, TopUpStacks, TopUpAmount,
            ShowItems, ShowPieces, ShowRecipes, FindRadius, Rows, Scale, BuildBox, Messages, PlaySounds,
        };
        var saved = new object[entries.Length];
        for (int i = 0; i < entries.Length; i++)
        {
            saved[i] = entries[i].BoxedValue;
            entries[i].BoxedValue = entries[i].DefaultValue;
        }

        return () =>
        {
            for (int i = 0; i < entries.Length; i++)
            {
                entries[i].BoxedValue = saved[i];
            }

            file.SaveOnConfigSet = saving;
        };
    }
}
