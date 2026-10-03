using System.Collections.Generic;
using Rummage.Core;
using Xunit;
using static Rummage.Tests.Fixtures;

namespace Rummage.Tests;

/// <summary>What the player reads after a grab.</summary>
public class ReportTests
{
    private static string Shown(string name) => name switch
    {
        Wood => "Wood",
        Stone => "Stone",
        Nails => "Bronze nails",
        _ => name,
    };

    private static IReadOnlyList<string> Lines(Plan plan, int skipped = 0)
    {
        var taken = new Dictionary<string, int>();
        foreach (var move in plan.Moves)
        {
            taken[move.Name] = taken.GetValueOrDefault(move.Name) + move.Amount;
        }

        return Report.Lines("Workbench", plan, taken, skipped, Shown);
    }

    [Fact]
    public void YouReadWhatWasTakenForWhat()
    {
        var plan = Plan(Inventory(), false, [Bring(Wood, 10), Bring(Stone, 2)], Chest(1, 5, Stack(Wood, 50), Stack(Stone, 9, x: 1)));

        Assert.Equal(["Workbench: took 10 Wood, 2 Stone"], Lines(plan));
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
            "8 Bronze nails short: 1 kept back, one in every chest; 2 kept in by chest rules; 1 in a chest out of reach; the rest is not in chests nearby",
        ], Lines(plan));
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
    public void TakesThatCouldNotBeMadeAreOwnedUpTo()
    {
        var plan = Plan(Inventory(), false, [Bring(Wood, 10)], Chest(1, 5, Stack(Wood, 50)));

        var lines = Report.Lines("Workbench", plan, new Dictionary<string, int>(), skipped: 1, Shown);

        Assert.Equal(["Workbench: took nothing", "1 take was not made because the chest changed meanwhile; press again"], lines);
    }
}
