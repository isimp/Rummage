using System.Collections.Generic;
using System.Linq;
using Rummage.Core;
using Xunit;
using static Rummage.Tests.Fixtures;

namespace Rummage.Tests;

/// <summary>
/// "New items": one of everything the chests nearby hold that the player has never carried, as
/// carrying an item once teaches it to the game and with it the recipes and pieces that use it.
/// </summary>
public class NewItemsTests
{
    private const string Thistle = "$item_thistle";
    private const string Resin = "$item_resin";
    private const string Amber = "$item_amber";

    private static readonly HashSet<string> Known = [Wood, Stone];

    private static bool IsKnown(string name) => Known.Contains(name);

    private static string Shown(string name) => name.Replace("$item_", "", System.StringComparison.Ordinal);

    [Fact]
    public void OneOfEachItemTheChestsHoldThatIsNewToThePlayer()
    {
        var chests = new[]
        {
            Chest(1, 5, Stack(Wood, 20), Stack(Thistle, 7, x: 1)),
            Chest(2, 6, Stack(Thistle, 3), Stack(Resin, 12, x: 1)),
        };

        var wants = NewItems.Wants(chests, IsKnown);
        var plan = Planner.Make(wants, chests, Inventory(), leaveOne: false);

        Assert.Equal([Thistle, Resin], wants.Select(w => w.Name));
        Assert.Equal(1, plan.Planned(Thistle));
        Assert.Equal(1, plan.Planned(Resin));
        Assert.Equal(0, plan.Planned(Wood));
        Assert.Equal(2, plan.Moves.Count);
    }

    [Fact]
    public void NothingIsTakenWhenEverythingNearbyIsKnown()
    {
        var wants = NewItems.Wants([Chest(1, 5, Stack(Wood, 20), Stack(Stone, 4, x: 1))], IsKnown);

        Assert.Empty(wants);
    }

    [Fact]
    public void TheNearestChestsItemsComeFirst()
    {
        var wants = NewItems.Wants(
        [
            Chest(1, 9, Stack(Amber, 1)),
            Chest(2, 3, Stack(Resin, 1)),
        ], IsKnown);

        Assert.Equal([Resin, Amber], wants.Select(w => w.Name));
    }

    [Fact]
    public void AnItemFromAnEarlierWorldLevelTeachesItAsWell()
    {
        var chests = new[] { Chest(1, 5, Stack(Thistle, 4, worldLevel: 0)) };

        var plan = Planner.Make(NewItems.Wants(chests, IsKnown), chests, Inventory(), leaveOne: false);

        Assert.Equal(1, plan.Planned(Thistle));
    }

    [Fact]
    public void LeaveOneItemKeepsTheLastOfAnItemAndTheReportSaysSo()
    {
        var chests = new[] { Chest(1, 5, Stack(Thistle, 1), Stack(Resin, 5, x: 1)) };

        var plan = Planner.Make(NewItems.Wants(chests, IsKnown), chests, Inventory(), leaveOne: true);
        var lines = Report.Lines(NewItems.Label, plan, new Done(new Dictionary<string, int> { [Resin] = 1 }, new Dictionary<string, int>(), 0, 0), Shown, canRoute: true);

        Assert.Equal(0, plan.Planned(Thistle));
        Assert.Equal(1, plan.Planned(Resin));
        Assert.Equal("New items: took 1 resin", lines[0]);
        Assert.Contains("1 thistle short: 1 kept back by Leave One Item", lines);
    }

    [Fact]
    public void ChestsThatMayNotBeUsedAreNotLookedInto()
    {
        var wants = NewItems.Wants(
        [
            Barred(1, 5, Bar.Private, Stack(Thistle, 4)),
            Barred(2, 5, Bar.InUse, Stack(Amber, 4)),
            Chest(3, 6, Stack(Resin, 2)),
        ], IsKnown);

        Assert.Equal([Resin], wants.Select(w => w.Name));
    }

    [Fact]
    public void AnItemOnlyInAChestAnotherPlayersGameHoldsIsNamedShortWithTheReason()
    {
        var chests = new[] { Chest(1, 5, Reach.Closed, Stack(Thistle, 4)) };

        var plan = Planner.Make(NewItems.Wants(chests, IsKnown), chests, Inventory(), leaveOne: false);
        var lines = Report.Lines(NewItems.Label, plan, new Done(new Dictionary<string, int>(), new Dictionary<string, int>(), 0, 0), Shown, canRoute: false);

        Assert.Equal(0, plan.Moves.Count);
        Assert.Contains("1 thistle short: 4 in a chest another player's game holds, which needs MultiUserChest", lines);
    }

    [Fact]
    public void AChestOfAnotherPlayersGameIsAskedForItsNewItems()
    {
        var chests = new[] { Chest(1, 5, Reach.Routed, Stack(Thistle, 4)) };

        var plan = Planner.Make(NewItems.Wants(chests, IsKnown), chests, Inventory(), leaveOne: false);

        Assert.Equal(Reach.Routed, Assert.Single(plan.Moves).Reach);
    }

    [Fact]
    public void TheSearchBoxRowCountsWhatCanBeTakenAgainstWhatIsNearby()
    {
        var count = NewItems.Count(
        [
            Chest(1, 5, Stack(Thistle, 1), Stack(Resin, 5, x: 1), Stack(Wood, 9, x: 2)),
            Chest(2, 6, prefab => prefab != "amber", Stack(Amber, 3)),
            Chest(3, 7, Reach.Closed, Stack(Amber, 3), Stack("$item_flint", 2, x: 1)),
            Barred(4, 5, Bar.Private, Stack("$item_coins", 99)),
        ], IsKnown, leaveOne: true);

        Assert.Equal(1, count.Takeable);
        Assert.Equal(4, count.Seen);
    }

    [Theory]
    [InlineData(3, 3, "3 new to you")]
    [InlineData(2, 5, "2 to take, 5 new nearby")]
    [InlineData(0, 4, "4 new nearby, none to take")]
    public void TheSearchBoxRowSaysHowManyAreNew(int takeable, int seen, string expected)
        => Assert.Equal(expected, Readout.NewItems(takeable, seen));

    [Fact]
    public void TheChosenRowNamesWhatItWouldTake()
    {
        Assert.Equal("One each of Amber, Resin, Thistle", Readout.NewNames(["Amber", "Resin", "Thistle"]));
        Assert.Equal("One each of Amber, Flint, Resin, Thistle and 2 more", Readout.NewNames(["Amber", "Flint", "Resin", "Thistle", "Coins", "Bones"]));
    }

    [Fact]
    public void WithNothingTypedTheNewItemsRowComesFirst()
    {
        Entry[] entries =
        [
            new(Wood, "Wood", EntryKind.Item, inChests: 120),
            new(NewItems.Key, NewItems.Label, EntryKind.New, inChests: 2, held: 3),
            new(Stone, "Amber", EntryKind.Item, inChests: 5),
        ];

        Assert.Equal([NewItems.Label, "Amber", "Wood"], Search.Rank("", entries).Select(e => e.Shown));
        Assert.Equal(NewItems.Label, Search.Rank("new", entries)[0].Shown);
    }
}
