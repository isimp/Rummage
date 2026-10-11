using BepInEx;

namespace Rummage.SelfTest;

/// <summary>
/// The self-test as a plugin of its own beside Rummage. Typing /rummage selftest in chat runs it
/// next to the player, with chests of its own.
/// </summary>
[BepInPlugin(Guid, "Rummage self-test", Plugin.Version)]
[BepInDependency(Plugin.Guid)]
[BepInProcess("valheim.exe")]
public sealed class SelfTestPlugin : BaseUnityPlugin
{
    public const string Guid = "isimp.Rummage.SelfTest";

    private bool _running;

    private void Awake()
    {
        Rummage.Game.Commands.Add("selftest", "runs Rummage's self-test with chests of its own next to you", args =>
        {
            if (_running)
            {
                args.Context?.AddString("The Rummage self-test is already running.");
                return;
            }

            if (!Player.m_localPlayer || Player.m_localPlayer.IsDead())
            {
                args.Context?.AddString("The Rummage self-test runs in a world, with you alive in it.");
                return;
            }

            StartCoroutine(Run());
        });
    }

    private System.Collections.IEnumerator Run()
    {
        _running = true;
        var test = new Tests(Logger);
        yield return test.Run();
        _running = false;
    }
}
