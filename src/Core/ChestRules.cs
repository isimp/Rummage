using System.Collections.Generic;
using System.Linq;

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

    /// <summary>
    /// The chests AzuCraftyBoxes counts as its own now. It takes a chest up, or drops it, into a
    /// list of its own first and merges that list into its registry only when it next looks for
    /// chests itself, so a chest built or loaded since then is among the ones being added.
    /// </summary>
    public static IEnumerable<T> Registered<T>(IEnumerable<T> listed, IEnumerable<T> adding, IEnumerable<T> dropping)
    {
        var dropped = new HashSet<T>(dropping);
        return listed.Concat(adding).Distinct().Where(chest => !dropped.Contains(chest));
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
