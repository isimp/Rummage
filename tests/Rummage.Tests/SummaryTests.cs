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

    [Fact]
    public void TheBuildPanelShowsYourBagAndWhatThePressWouldBring()
    {
        var plan = Plan(Inventory(Stack(Wood, 7)), false, [Part(Wood, 10), Part(Stone, 4), Part(Cores, 2)],
            Chest(1, 5, Stack(Wood, 50), Stack(Stone, 50, x: 1)));

        Assert.Equal(
            "In your bag: <color=#FFA13C>Wood 7/10</color> · <color=#FFA13C>Stone 0/4</color> · <color=#E8604C>Surtling core 0/2</color>\n"
            + "J brings 3 Wood, 4 Stone · short: Surtling core",
            Readout.Panel(plan, 1, "J", Shown));
    }

    [Fact]
    public void ForMoreThanOneThePanelSaysHowMany()
    {
        var plan = Plan(Inventory(Stack(Wood, 10)), false, [Part(Wood, 20)], Chest(1, 5, Stack(Wood, 50)));

        Assert.Equal("For 2 · in your bag: <color=#FFA13C>Wood 10/20</color>\nJ brings 10 Wood", Readout.Panel(plan, 2, "J", Shown));
    }

    [Fact]
    public void WithEverythingCarriedThePanelSaysSo()
    {
        var plan = Plan(Inventory(Stack(Wood, 10)), false, [Part(Wood, 10)], Chest(1, 5, Stack(Wood, 50)));

        Assert.Equal("In your bag: Wood 10/10\nYou carry it all", Readout.Panel(plan, 1, "J", Shown));
    }

    [Fact]
    public void WhenThePressWouldBringNothingThePanelSaysWhatIsShort()
    {
        var plan = Plan(Inventory(), false, [Part(Cores, 2)]);

        Assert.Equal("In your bag: <color=#E8604C>Surtling core 0/2</color>\nJ brings nothing · short: Surtling core", Readout.Panel(plan, 1, "J", Shown));
    }

    [Fact]
    public void TheSearchBoxShowsEachPartCarriedAndInTheChestsAgainstTheCount()
    {
        var cost = new Dictionary<string, int> { [Wood] = 10, [Cores] = 2 };
        var carried = new Dictionary<string, int> { [Wood] = 12 };
        var takeable = new Dictionary<string, int> { [Wood] = 40, [Cores] = 1 };

        Assert.Equal("Wood 12 + 40 / 20 · Surtling core 0 + 1 / 4 (short)", Readout.Parts(cost, 2, carried, takeable, Shown));
    }

    [Fact]
    public void TheSearchBoxShowsAnItemCarriedAndInTheChests()
    {
        Assert.Equal("12 carried + 98 in chests", Readout.Item(12, 98));
    }
}
