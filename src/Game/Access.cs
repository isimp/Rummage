namespace Rummage.Game;

/// <summary>
/// Whether the player could open a chest by hand right now, asked the way the game asks when a
/// chest is opened: the wards covering it, through the game's own ward check (and so by the rules
/// of any mod that changes that check), and the chest's privacy. AzuCraftyBoxes checks this only
/// when it first lists a chest, so a chest that became off-limits later is still on its list;
/// Rummage asks again before it takes from, finds in or counts a chest.
/// </summary>
internal static class Access
{
    public static bool MayOpen(Container chest, Player player)
    {
        if (!chest || !player)
        {
            return false;
        }

        if (chest.m_checkGuardStone && !PrivateArea.CheckAccess(chest.transform.position, 0f, flash: false, wardCheck: false))
        {
            return false;
        }

        switch (chest.m_privacy)
        {
            case Container.PrivacySetting.Public:
                return true;
            case Container.PrivacySetting.Private:
                var piece = chest.GetComponentInParent<Piece>();
                return piece && piece.GetCreator() == player.GetPlayerID();
            default:
                return false;
        }
    }
}
