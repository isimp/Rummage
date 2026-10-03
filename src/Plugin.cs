using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Rummage.Game;
using UnityEngine;

namespace Rummage;

[BepInPlugin(Guid, Name, Version)]
[BepInDependency(CraftyLink.Guid)]
[BepInDependency(MucLink.Guid, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(ExtraSlotsLink.Guid, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(CraftAmountLink.Guid, BepInDependency.DependencyFlags.SoftDependency)]
[BepInProcess("valheim.exe")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA1001", Justification = "Unity ends a plugin with OnDestroy, which unpatches; a MonoBehaviour is never disposed.")]
public sealed class Plugin : BaseUnityPlugin
{
    public const string Guid = "isimp.Rummage";
    public const string Name = "Rummage";
    public const string Version = "0.1.0";

    private readonly Grabber _grabber = new();
    private Palette _palette;
    private ConfigEntry<KeyCode> _key;
    private Harmony _harmony;

    internal static ManualLogSource Log { get; private set; }

    internal static Plugin Instance { get; private set; }

    internal Grabber Grabber => _grabber;

    internal Palette Palette => _palette;

    private void Awake()
    {
        Log = Logger;
        Instance = this;
        _palette = new Palette(_grabber);
        _key = Config.Bind("Keys", "Key", KeyCode.J,
            "Takes what the thing in front of you needs from the chests around you: the recipe selected in the crafting tab, "
            + "the build piece under the cursor in the build menu, or the piece you are placing. With nothing of the sort, it opens the search box; "
            + "with Shift, it opens the search box starting from that thing. Ignored while Alt is held, so Alt with the same key stays free for other mods.");
        _harmony = new Harmony(Guid);
        _harmony.PatchAll(typeof(Plugin).Assembly);
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
        _grabber.Tick(Time.time);

        if (Palette.IsOpen || !Input.GetKeyDown(_key.Value) || Held(KeyCode.LeftAlt, KeyCode.RightAlt, KeyCode.AltGr))
        {
            return;
        }

        var player = Player.m_localPlayer;
        if (!Guard.MayAct(player))
        {
            return;
        }

        if (!CraftyLink.Ready)
        {
            player.Message(MessageHud.MessageType.TopLeft, CraftyLink.Problem);
            return;
        }

        var target = Context.Find(player);
        if (target != null && !Held(KeyCode.LeftShift, KeyCode.RightShift))
        {
            _grabber.Grab(player, target, Time.time);
        }
        else
        {
            _palette.Open(player, target);
        }
    }

    private void OnGUI() => _palette.OnGUI();

    private void OnDestroy()
    {
        if (Palette.IsOpen)
        {
            _palette.Close();
        }

        _harmony?.UnpatchSelf();
    }

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
