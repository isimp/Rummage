using System.Collections.Generic;
using Rummage.Core;
using Xunit;
using static Rummage.Tests.Fixtures;

namespace Rummage.Tests;

/// <summary>
/// What you carry for a piece or recipe: after a press, while a piece is selected, and in the
/// search box.
/// </summary>
public class SummaryTests
{
    private const string Cores = "$item_surtlingcore";

    private static string Shown(string name) => name switch
    {
        Wood => "Wood",
        Stone => "Stone",
        Cores => "Surtling core",
        _ => name,
    };

    private static Want Part(string name, int count) => MakeSureICarry(name, count);

    private static IReadOnlyList<string> Pressed(Plan plan)
    {
        var took = new Dictionary<string, int>();
        var asked = new Dictionary<string, int>();
        foreach (var move in plan.Moves)
        {
            var into = move.Reach == Reach.Routed ? asked : took;
            into[move.Name] = into.GetValueOrDefault(move.Name) + move.Amount;
        }

        return Report.Lines("Portal", plan, new Done(took, asked, 0, 0), Shown, true);
    }

    [Fact]
    public void AfterAPressEachPartShowsWhatYouCarryOfWhatItNeeds()
    {
        var plan = Plan(Inventory(Stack(Stone, 1)), false, [Part(Wood, 10), Part(Stone, 4), Part(Cores, 2)],
            Chest(1, 5, Stack(Wood, 50), Stack(Stone, 50, x: 1)));

        Assert.Equal(
        [
            "Portal: Wood 10/10 (+10) · Stone 4/4 (+3) · Surtling core 0/2",
            "2 Surtling core short: not in chests nearby",
        ], Pressed(plan));
    }

    [Fact]
    public void WhatIsOnItsWayFromAnotherPlayersChestIsShownApart()
    {
        var plan = Plan(Inventory(), false, [Part(Wood, 10)], Chest(1, 5, Reach.Routed, Stack(Wood, 50)));

        Assert.Equal(["Portal: Wood 0/10 (+10 on the way)"], Pressed(plan));
    }

    [Fact]
    public void WhenEverythingIsCarriedTheSummarySaysSo()
    {
        var plan = Plan(Inventory(Stack(Wood, 12)), false, [Part(Wood, 10)], Chest(1, 5, Stack(Wood, 50)));

        Assert.Equal(["Portal: Wood 12/10 · you carry it all"], Pressed(plan));
    }

    /// <summary>Text width for the panel's columns: ten units a character.</summary>
    private static float Width(string text) => text.Length * 10f;

    private static string[] PanelRows(Plan plan, int count) => Readout.Panel(plan, count, "J", Shown, Width).Split('\n');

    [Fact]
    public void TheBuildPanelIsATableOfWhatYouCarryAndWhatTheKeyTakes()
    {
        var plan = Plan(Inventory(Stack(Wood, 7)), false, [Part(Wood, 10), Part(Stone, 4)],
            Chest(1, 5, Stack(Wood, 50), Stack(Stone, 50, x: 1)));

        // The names column is as wide as "For 1" or the longest name, the carried column as wide as
        // "carried" or its longest entry, each with a gap of 24 after it.
        Assert.Equal(
        [
            "<color=#A89F91>For 1<pos=74>carried<pos=168>J takes</color>",
            "Wood<pos=74><color=#FFA13C>7 / 10</color><pos=168><color=#FFA13C>3</color>",
            "Stone<pos=74><color=#FFA13C>0 / 4</color><pos=168><color=#FFA13C>4</color>",
        ], PanelRows(plan, 1));
    }

    [Fact]
    public void APartTheChestsCannotGiveSaysNone()
    {
        var plan = Plan(Inventory(Stack(Stone, 20), Stack(Cores, 5, x: 1)), false, [Part(Stone, 40), Part(Cores, 10)]);

        Assert.Equal(
        [
            "<color=#A89F91>For 2<pos=154>carried<pos=248>J takes</color>",
            "Stone<pos=154><color=#E8604C>20 / 40</color><pos=248><color=#E8604C>none</color>",
            "Surtling core<pos=154><color=#E8604C>5 / 10</color><pos=248><color=#E8604C>none</color>",
        ], PanelRows(plan, 2));
    }

    [Fact]
    public void APartTheChestsGiveOnlySomeOfSaysHowManyStayShort()
    {
        var plan = Plan(Inventory(), false, [Part(Stone, 4)], Chest(1, 5, Stack(Stone, 2)));

        Assert.Equal("Stone<pos=74><color=#E8604C>0 / 4</color><pos=168><color=#FFA13C>2</color> · <color=#E8604C>2 short</color>",
            PanelRows(plan, 1)[1]);
    }

    [Fact]
    public void WithEverythingCarriedThePanelSaysSoAndTakesNothing()
    {
        var plan = Plan(Inventory(Stack(Wood, 10), Stack(Stone, 4, x: 1)), false, [Part(Wood, 10), Part(Stone, 4)],
            Chest(1, 5, Stack(Wood, 50)));

        Assert.Equal(
        [
            "<color=#A89F91>For 1<pos=74>carried<pos=168>J takes</color>",
            "Wood<pos=74>10 / 10<pos=168>",
            "Stone<pos=74>4 / 4<pos=168>",
            "You carry it all",
        ], PanelRows(plan, 1));
    }

    [Fact]
    public void ThePanelNamesTheKeyAsSet()
    {
        var plan = Plan(Inventory(), false, [Part(Wood, 10)], Chest(1, 5, Stack(Wood, 50)));

        Assert.StartsWith("<color=#A89F91>For 1<pos=74>carried<pos=168>F7 takes</color>", Readout.Panel(plan, 1, "F7", Shown, Width));
    }

    private static string[] FullSetRows(Plan plan, Dictionary<string, int> carried) => Readout.FullSetPanel(plan, carried, "J", Shown, Width).Split('\n');

    [Fact]
    public void WithFullSetTheBuildBoxShowsWhatYouCarryAndTheWholeSetTheKeyTakes()
    {
        var plan = Plan(Inventory(Stack(Wood, 7)), false, [Bring(Wood, 10), Bring(Stone, 4)],
            Chest(1, 5, Stack(Wood, 50), Stack(Stone, 50, x: 1)));

        // "One more" is the widest entry of the names column, "carried" of the carried column.
        Assert.Equal(
        [
            "<color=#A89F91>One more<pos=104>carried<pos=198>J takes</color>",
            "Wood<pos=104>7<pos=198><color=#FFA13C>10</color>",
            "Stone<pos=104>0<pos=198><color=#FFA13C>4</color>",
        ], FullSetRows(plan, new() { [Wood] = 7 }));
    }

    [Fact]
    public void WithFullSetAPartTheChestsLackIsStillMarked()
    {
        var plan = Plan(Inventory(), false, [Bring(Stone, 4), Bring(Cores, 2)], Chest(1, 5, Stack(Stone, 2)));

        string[] rows = FullSetRows(plan, []);
        Assert.EndsWith("<color=#FFA13C>2</color> · <color=#E8604C>2 short</color>", rows[1]);
        Assert.EndsWith("<color=#E8604C>none</color>", rows[2]);
    }

    [Fact]
    public void WithFullSetTheSearchBoxShowsWhatTheChestsGiveAgainstTheWholeSets()
    {
        var cost = new Dictionary<string, int> { [Wood] = 10, [Cores] = 2 };
        var carried = new Dictionary<string, int> { [Wood] = 12 };
        var takeable = new Dictionary<string, int> { [Wood] = 10, [Cores] = 1 };

        // The 12 wood carried would make up 20, but under FullSet the chests have to give it all.
        Assert.Equal("Wood 10 / 20 (short) · Surtling core 1 / 4 (short)", Readout.Parts(cost, 2, carried, takeable, Shown, countsCarried: false));
    }

    [Fact]
    public void TheSearchBoxShowsEachPartCarriedAndInTheChestsAgainstTheCount()
    {
        var cost = new Dictionary<string, int> { [Wood] = 10, [Cores] = 2 };
        var carried = new Dictionary<string, int> { [Wood] = 12 };
        var takeable = new Dictionary<string, int> { [Wood] = 40, [Cores] = 1 };

        Assert.Equal("Wood 12 + 40 / 20 · Surtling core 0 + 1 / 4 (short)", Readout.Parts(cost, 2, carried, takeable, Shown));
    }

    [Theory]
    [InlineData(98, 98, "98 in chests")]
    [InlineData(19, 20, "19 to take, 20 nearby")]
    [InlineData(0, 1, "1 nearby, none to take")]
    [InlineData(5, 0, "5 in chests")]
    [InlineData(0, 0, "none nearby")]
    public void AnItemRowSaysWhetherThereIsAnythingToTake(int takeable, int held, string shown)
    {
        Assert.Equal(shown, Readout.Stocked(takeable, held));
    }

    [Fact]
    public void TheSearchBoxShowsAnItemCarriedAndInTheChests()
    {
        Assert.Equal("12 carried + 98 in chests", Readout.Item(12, 98));
    }
}
