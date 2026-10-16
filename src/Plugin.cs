using BepInEx;
using HarmonyLib;
using Rummage.Game;
using UnityEngine;

namespace Rummage;

/// <summary>What a press of the key came to, for the self-test.</summary>
internal enum Pressed
{
    Took,
    Opened,
    Nothing,
}

[BepInPlugin(Guid, Name, Version)]
[BepInDependency(CraftyLink.Guid, CraftyLink.TestedVersion)]
[BepInDependency(MucLink.Guid, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(ExtraSlotsLink.Guid, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(CraftAmountLink.Guid, BepInDependency.DependencyFlags.SoftDependency)]
[BepInProcess("valheim.exe")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA1001", Justification = "Unity ends a plugin with OnDestroy, which unpatches; a MonoBehaviour is never disposed.")]
public sealed class Plugin : BaseUnityPlugin
{
    public const string Guid = "isimp.Rummage";
    public const string Name = "Rummage";
    public const string Version = "0.3.0";

    private readonly Grabber _grabber = new();
    private readonly BuildReadout _readout = new();
    private Palette _palette;
    private PaletteView _view;
    private Harmony _harmony;

    internal static BepInEx.Logging.ManualLogSource Log { get; private set; }

    internal static Plugin Instance { get; private set; }

    internal Grabber Grabber => _grabber;

    internal Palette Palette => _palette;

    internal BuildReadout BuildLine => _readout;

    private void Awake()
    {
        Log = Logger;
        Instance = this;
        Settings.Bind(Config);
        _palette = new Palette(_grabber);
        _view = gameObject.AddComponent<PaletteView>();
        _view.Palette = _palette;
        _view.enabled = false;
        _harmony = new Harmony(Guid);
        _harmony.PatchAll(typeof(Plugin).Assembly);
        Commands.Add("new", "takes one of every item in the chests nearby you have never carried", _ => TakeNew(Player.m_localPlayer));
        Commands.Add("forget", "forgets what the search box last found or took for this character", args =>
        {
            LastUsed.Forget();
            args.Context?.AddString("Rummage: the search box no longer remembers what it last found or took for this character.");
        });
    }

    private void Start()
    {
        CraftyLink.Init();
        MucLink.Init();
        ExtraSlotsLink.Init();
        CraftAmountLink.Init();
        Log.LogInfo($"Rummage {Version}: chests from AzuCraftyBoxes {(CraftyLink.Ready ? "found" : "MISSING")}, "
            + $"other players' chests {(MucLink.Ready ? "through MultiUserChest" : "left alone")}.");
    }

    private void Update()
    {
        Sound.Prepare();
        _grabber.Tick(Time.time);
        Highlighter.Tick(Time.time);
        _readout.Tick(Player.m_localPlayer, Time.time, Settings.Key.Value.ToString(), Settings.TakeForPieces.Value ? Settings.BuildBox.Value : Core.BuildBox.Never);
        bool drawing = Palette.IsOpen || (!Skin.Warmed && Player.m_localPlayer);
        if (_view.enabled != drawing)
        {
            _view.enabled = drawing;
        }

        if (Palette.IsOpen || Held(KeyCode.LeftAlt, KeyCode.RightAlt, KeyCode.AltGr))
        {
            return;
        }

        var searchKey = Settings.SearchKey.Value;
        if (searchKey != KeyCode.None && Input.GetKeyDown(searchKey))
        {
            Press(Player.m_localPlayer, search: true);
        }
        else if (Input.GetKeyDown(Settings.Key.Value))
        {
            Press(Player.m_localPlayer, search: ModifierHeld());
        }
    }

    /// <summary>
    /// A press of the key: takes for the thing in front of the player, or opens the search box,
    /// starting from that thing, when <paramref name="search"/> asks for it or there is nothing to
    /// take for and the settings let the key open it.
    /// </summary>
    internal Pressed Press(Player player, bool search)
    {
        if (!Guard.MayAct(player))
        {
            return Pressed.Nothing;
        }

        if (!CraftyLink.Ready)
        {
            Notice.Say(player, CraftyLink.Problem);
            return Pressed.Nothing;
        }

        try
        {
            var target = Context.Find(player);
            if (target != null && !search)
            {
                _grabber.Grab(player, target, Time.time);
                _readout.Refresh();
                return Pressed.Took;
            }

            if (target == null && !search && !Settings.KeyAloneOpensSearch.Value)
            {
                return Pressed.Nothing;
            }

            _palette.Open(player, target);
            return Pressed.Opened;
        }
        catch (System.Exception e)
        {
            Log.LogError($"The key failed: {e}");
            Notice.Say(player, "Rummage ran into an error; see the log");
            return Pressed.Nothing;
        }
    }

    /// <summary>/rummage new: one of every item in the chests nearby the player has never carried.</summary>
    internal void TakeNew(Player player)
    {
        if (!player || player.IsDead() || Palette.IsOpen)
        {
            return;
        }

        if (!CraftyLink.Ready)
        {
            Notice.Say(player, CraftyLink.Problem);
            return;
        }

        try
        {
            _grabber.GrabNew(player, Time.time);
            _readout.Refresh();
        }
        catch (System.Exception e)
        {
            Log.LogError($"Taking new items failed: {e}");
            Notice.Say(player, "Rummage ran into an error; see the log");
        }
    }

    private void OnDestroy()
    {
        Sound.Mute();
        if (Palette.IsOpen)
        {
            _palette.Close();
        }

        _readout.Remove();
        _harmony?.UnpatchSelf();
    }

    private static bool ModifierHeld() => Settings.Modifier.Value switch
    {
        SearchModifier.Shift => Held(KeyCode.LeftShift, KeyCode.RightShift),
        SearchModifier.Control => Held(KeyCode.LeftControl, KeyCode.RightControl),
        _ => false,
    };

    private static bool Held(params KeyCode[] keys)
    {
        foreach (var key in keys)
        {
            if (Input.GetKey(key))
            {
                return true;
            }
        }

        return false;
    }
}
