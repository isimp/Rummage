namespace Rummage.Core;

/// <summary>A chest's privacy setting, as far as it decides who may open it.</summary>
internal enum ChestPrivacy
{
    /// <summary>Anyone the ward lets in.</summary>
    Public,

    /// <summary>Only the player who built the chest.</summary>
    Private,

    /// <summary>Any other setting, the game's group setting among them: nobody.</summary>
    Other,
}

/// <summary>Who may use a chest and how, by the game's rules, with no game in it; the game side reads the facts.</summary>
internal static class ChestRules
{
    /// <summary>
    /// Whether the player could open the chest by hand, asked the way the game asks: the ward
    /// covering it, where the chest checks wards, and its privacy.
    /// </summary>
    public static bool MayOpen(bool checksWard, bool wardLetsIn, ChestPrivacy privacy, bool playerBuiltIt)
    {
        if (checksWard && !wardLetsIn)
        {
            return false;
        }

        return privacy switch
        {
            ChestPrivacy.Public => true,
            ChestPrivacy.Private => playerBuiltIt,
            _ => false,
        };
    }

    /// <summary>Why a chest in reach that may not be used is barred: in use, someone else's private chest, or kept out otherwise.</summary>
    public static Bar BarOf(bool openElsewhere, bool cartPulled, ChestPrivacy privacy, bool playerBuiltIt)
    {
        if (openElsewhere || cartPulled)
        {
            return Bar.InUse;
        }

        return privacy == ChestPrivacy.Private && !playerBuiltIt ? Bar.Private : Bar.Locked;
    }

    /// <summary>
    /// How a chest may be taken from: one this game holds saves what changes in it; one another
    /// game holds only through MultiUserChest, which asks that game; without it not at all.
    /// </summary>
    public static Reach ReachOf(bool held, bool routed) => held ? Reach.Owned : routed ? Reach.Routed : Reach.Closed;

    /// <summary>Whether a carried item counts for building and crafting: only one made for the world's level or above.</summary>
    public static bool Counted(int itemWorldLevel, int worldLevel) => itemWorldLevel >= worldLevel;
}
