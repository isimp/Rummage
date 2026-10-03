using System.Collections.Generic;
using Rummage.Core;
using Xunit;
using static Rummage.Tests.Fixtures;

namespace Rummage.Tests;

/// <summary>What the player reads after a grab, and after other players' chests have answered.</summary>
public class ReportTests
{
    private static string Shown(string name) => name switch
    {
        Wood => "Wood",
        Stone => "Stone",
        Nails => "Bronze nails",
        _ => name,
    };

    /// <summary>The lines for a plan carried out as planned.</summary>
    private static IReadOnlyList<string> Lines(Plan plan, bool canRoute = true)
    {
        var took = new Dictionary<string, int>();
        var asked = new Dictionary<string, int>();
        foreach (var move in plan.Moves)
        {
            var into = move.Reach == Reach.Routed ? asked : took;
            into[move.Name] = into.GetValueOrDefault(move.Name) + move.Amount;
        }

        return Report.Lines("Workbench", plan, new Done(took, asked, changed: 0, refused: 0), Shown, canRoute);
    }

    [Fact]
    public void YouReadWhatWasTakenForWhat()
    {
        var plan = Plan(Inventory(), false, [Bring(Wood, 10), Bring(Stone, 2)], Chest(1, 5, Stack(Wood, 50), Stack(Stone, 9, x: 1)));

        Assert.Equal(["Workbench: took 10 Wood, 2 Stone"], Lines(plan));
    }

    [Fact]
    public void WhatIsAskedOfAnotherPlayersChestIsNotCalledTakenYet()
    {
        var plan = Plan(Inventory(), false, [Bring(Wood, 10), Bring(Stone, 2)],
            Chest(1, 5, Stack(Stone, 9)),
            Chest(2, 6, Reach.Routed, Stack(Wood, 50)));

        Assert.Equal(["Workbench: took 2 Stone; asked another player's chest for 10 Wood"], Lines(plan));
    }

    [Fact]
    public void OnlyAskingSaysSo()
    {
        var plan = Plan(Inventory(), false, [Bring(Wood, 10)], Chest(2, 6, Reach.Routed, Stack(Wood, 50)));

        Assert.Equal(["Workbench: asked another player's chest for 10 Wood"], Lines(plan));
    }

    [Fact]
    public void WhenYouCarryEverythingAlreadyYouAreToldSo()
    {
        var plan = Plan(Inventory(Stack(Wood, 10)), false, [MakeSureICarry(Wood, 10)], Chest(1, 5, Stack(Wood, 50)));

        Assert.Equal(["Workbench: you carry everything already"], Lines(plan));
    }

    [Fact]
    public void EachShortfallSaysWhyTheItemsAreMissing()
    {
        var plan = Plan(Inventory(), true, [Bring(Nails, 10)],
            Chest(1, 5, Stack(Nails, 3, maxStack: 100)),
            Chest(2, 6, prefab => false, Stack(Nails, 2, maxStack: 100)),
            Chest(3, 7, Reach.Closed, Stack(Nails, 1, maxStack: 100)));

        Assert.Equal(
        [
            "Workbench: took 2 Bronze nails",
            "8 Bronze nails short: 1 kept back by Leave One Item; 2 kept in by chest rules; 1 in a chest another player's game holds; the rest is not in chests nearby",
        ], Lines(plan));
    }

    [Fact]
    public void WithoutMultiUserChestTheReportSaysWhatWouldOpenAnotherPlayersChest()
    {
        var plan = Plan(Inventory(), false, [Bring(Wood, 10)], Chest(3, 7, Reach.Closed, Stack(Wood, 50)));

        Assert.Equal(
        [
            "Workbench: took nothing",
            "10 Wood short: 50 in a chest another player's game holds, which needs MultiUserChest",
        ], Lines(plan, canRoute: false));
    }

    [Fact]
    public void AChestSomeoneHasOpenIsNamedWithItsCount()
    {
        var plan = Plan(Inventory(), false, [Bring(Wood, 10)], Barred(4, 3, Bar.InUse, Stack(Wood, 30)));

        Assert.Equal(["Workbench: took nothing", "10 Wood short: 30 in a chest someone has open"], Lines(plan));
    }

    [Fact]
    public void SomeonesPrivateChestIsNamedWithoutWhatItHolds()
    {
        var plan = Plan(Inventory(), false, [Bring(Wood, 10)], Barred(4, 3, Bar.Private, Stack(Wood, 30)));

        Assert.Equal(["Workbench: took nothing", "10 Wood short: some in someone's private chest"], Lines(plan));
    }

    [Fact]
    public void AChestYouMayNotTakeFromIsNamedWithItsCount()
    {
        var plan = Plan(Inventory(), false, [Bring(Wood, 10)], Barred(4, 3, Bar.Locked, Stack(Wood, 30)));

        Assert.Equal(["Workbench: took nothing", "10 Wood short: 30 in chests you may not take from"], Lines(plan));
    }

    [Fact]
    public void BarredChestsGiveNothingAndCountNowhereElse()
    {
        var plan = Plan(Inventory(), false, [Bring(Wood, 10)],
            Barred(4, 1, Bar.InUse, Stack(Wood, 30)),
            Chest(1, 5, Stack(Wood, 4)));

        Assert.Equal(4, plan.Planned(Wood));
        Assert.Equal(0, Stock.Takeable([Barred(4, 1, Bar.InUse, Stack(Wood, 30))], false, 0).GetValueOrDefault(Wood));
        Assert.Equal(30, plan.For(Wood).InUse);
    }

    [Fact]
    public void AFullInventorySaysSo()
    {
        var plan = Plan(Inventory(1, 1, Stack(Wood, 45)), false, [Bring(Wood, 10)], Chest(1, 5, Stack(Wood, 50)));

        Assert.Equal(["Workbench: took 5 Wood", "5 Wood short: no room left in your inventory"], Lines(plan));
    }

    [Fact]
    public void NothingFoundIsSaidPlainly()
    {
        var plan = Plan(Inventory(), false, [Bring(Wood, 10)]);

        Assert.Equal(["Workbench: took nothing", "10 Wood short: not in chests nearby"], Lines(plan));
    }

    [Fact]
    public void TakesThatCouldNotBeMadeAreOwnedUpToWithTheRightReason()
    {
        var plan = Plan(Inventory(), false, [Bring(Wood, 10)], Chest(1, 5, Stack(Wood, 50)));

        var lines = Report.Lines("Workbench", plan, new Done(new Dictionary<string, int>(), new Dictionary<string, int>(), changed: 1, refused: 2), Shown, true);

        Assert.Equal(
        [
            "Workbench: took nothing",
            "1 take was not made because the chest changed meanwhile; press again",
            "2 takes were turned down by another player's chest; press again",
        ], lines);
    }

    [Fact]
    public void WhatArrivesFromAnotherPlayersChestIsReportedWhenItArrives()
    {
        var lines = Report.Arrivals([new Arrival(Wood, 10, 10, answered: true), new Arrival(Stone, 4, 4, answered: true)], Shown);

        Assert.Equal(["10 Wood arrived", "4 Stone arrived"], lines);
    }

    [Fact]
    public void WhatNeverArrivesIsReportedWithWhy()
    {
        var lines = Report.Arrivals(
        [
            new Arrival(Wood, 10, 6, answered: true),
            new Arrival(Wood, 5, 0, answered: true),
            new Arrival(Stone, 4, 0, answered: false),
        ], Shown);

        Assert.Equal(
        [
            "6 Wood arrived",
            "9 Wood did not come: the other chest's game turned it down",
            "4 Stone did not come: the other chest's game did not answer",
        ], lines);
    }

    [Fact]
    public void MoreThanAskedCountsAsWhatWasAsked()
    {
        var lines = Report.Arrivals([new Arrival(Wood, 5, 9, answered: true)], Shown);

        Assert.Equal(["5 Wood arrived"], lines);
    }
}
