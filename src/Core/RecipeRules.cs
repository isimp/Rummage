namespace Rummage.Core;

/// <summary>What one craft costs and makes, by the game's rules, with no game in it.</summary>
internal static class RecipeRules
{
    /// <summary>
    /// Whether a recipe's part counts for a craft: it has an amount at that quality; at a crafting
    /// station only parts of the station's kind count (an upgrade station's own parts only at such
    /// a station, the ordinary ones elsewhere), away from one only the ordinary ones; and for a
    /// recipe that takes any one ingredient, only those the player knows.
    /// </summary>
    public static bool Counts(int amount, bool atStation, bool stationUpgrades, bool upgraderPart, bool onlyOne, bool known)
    {
        bool stationFits = atStation ? stationUpgrades == upgraderPart : !upgraderPart;
        return amount > 0 && stationFits && (!onlyOne || known);
    }

    /// <summary>
    /// What the crafting tab makes: a new item at quality 1, as many times as its amount says;
    /// an upgrade of an item at <paramref name="upgrading"/> at the next level, once.
    /// </summary>
    public static (int Quality, int Count) Craft(int? upgrading, int craftAmount) =>
        upgrading == null ? (1, craftAmount) : (upgrading.Value + 1, 1);

    /// <summary>The key a recipe is remembered by: the search box lists a recipe at its first quality, so only that one has one.</summary>
    public static string KeyOf(int quality, string name) => quality == 1 ? name : null;
}
