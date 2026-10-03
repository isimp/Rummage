using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Rummage.Game;
using UnityEngine;

namespace Rummage;

[BepInPlugin(Guid, Name, Version)]
[BepInDependency(CraftyLink.Guid)]
[BepInDependency(MucLink.Guid, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(ExtraSlotsLink.Guid, BepInDependency.DependencyFlags.SoftDependency)]
[BepInProcess("valheim.exe")]
public sealed class Plugin : BaseUnityPlugin
{
    public const string Guid = "isimp.Rummage";
    public const string Name = "Rummage";
    public const string Version = "0.1.0";

    private readonly Grabber _grabber = new();
    private ConfigEntry<KeyCode> _grabKey;

    internal static ManualLogSource Log { get; private set; }

    private void Awake()
    {
        Log = Logger;
        _grabKey = Config.Bind("Keys", "GrabKey", KeyCode.J,
            "Takes what the build piece costs from the chests around you: the piece under the cursor in the build menu, or the one you are placing. "
            + "Ignored while Alt is held, so Alt with the same key stays free for other mods.");
    }

    private void Start()
    {
        CraftyLink.Init();
        MucLink.Init();
        ExtraSlotsLink.Init();
        Log.LogInfo($"Rummage {Version}: chests from AzuCraftyBoxes {(CraftyLink.Ready ? "found" : "MISSING")}, "
            + $"other players' chests {(MucLink.Ready ? "through MultiUserChest" : "left alone")}.");
    }

    private void Update()
    {
        _grabber.Tick(Time.time);

        if (!CraftyLink.Ready || !Input.GetKeyDown(_grabKey.Value) || AltHeld())
        {
            return;
        }

        var player = Player.m_localPlayer;
        if (!Guard.MayAct(player))
        {
            return;
        }

        var target = Context.Find(player);
        if (target == null)
        {
            player.Message(MessageHud.MessageType.TopLeft, "Rummage: hover or select a build piece first");
            return;
        }

        _grabber.Grab(player, target, Time.time);
    }

    private static bool AltHeld() => Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt) || Input.GetKey(KeyCode.AltGr);
}
