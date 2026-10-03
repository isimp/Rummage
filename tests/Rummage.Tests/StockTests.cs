using System.Collections.Generic;
using Rummage.Core;
using Xunit;
using static Rummage.Tests.Fixtures;

namespace Rummage.Tests;

/// <summary>What the chests can give, as the search box shows it, and how many of something that adds up to.</summary>
public class StockTests
{
    [Fact]
    public void TheChestsGiveWhatAGrabWouldTake()
    {
        var stock = Stock.Takeable(
        [
            Chest(1, 5, Stack(Wood, 20), Stack(Stone, 1, x: 1)),
            Chest(2, 6, Reach.Closed, Stack(Wood, 50)),
            Chest(3, 7, prefab => prefab != "Wood", Stack(Wood, 9), Stack(Stone, 5, x: 1)),
            Chest(4, 8, Reach.Routed, Stack(Wood, 3)),
        ], leaveOne: true, minWorldLevel: 0);

        Assert.Equal(19 + 2, stock[Wood]);
        Assert.Equal(0 + 4, stock[Stone]);
    }

    [Fact]
    public void WithoutLeaveOneEverythingReachableCounts()
    {
        var stock = Stock.Takeable([Chest(1, 5, Stack(Wood, 20), Stack(Wood, 5, x: 1))], leaveOne: false, minWorldLevel: 0);

        Assert.Equal(25, stock[Wood]);
    }

    [Fact]
    public void ItemsBelowTheWorldLevelDoNotCount()
    {
        var stock = Stock.Takeable([Chest(1, 5, Stack(Wood, 20, worldLevel: 0), Stack(Wood, 5, x: 1, worldLevel: 1))], leaveOne: false, minWorldLevel: 1);

        Assert.Equal(5, stock[Wood]);
    }

    [Fact]
    public void ThePlanAndTheStockAgree()
    {
        Source[] chests =
        [
            Chest(1, 5, Stack(Wood, 20), Stack(Wood, 1, x: 1)),
            Chest(2, 6, prefab => false, Stack(Wood, 9)),
            Chest(3, 7, Stack(Wood, 1)),
        ];

        var stock = Stock.Takeable(chests, leaveOne: true, minWorldLevel: 0);
        var plan = Planner.Make([Bring(Wood, 1000)], chests, Inventory(), leaveOne: true);

        Assert.Equal(stock[Wood], plan.Planned(Wood));
    }

    [Fact]
    public void HowManyYouCanAffordIsSetByTheScarcestPart()
    {
        var cost = new Dictionary<string, int> { [Wood] = 10, [Stone] = 4 };
        var carried = new Dictionary<string, int> { [Wood] = 8 };
        var takeable = new Dictionary<string, int> { [Wood] = 12, [Stone] = 9 };

        Assert.Equal(2, Stock.Affordable(cost, carried, takeable));
    }

    [Fact]
    public void SomethingThatCostsNothingIsNotCountedAsEndless()
    {
        Assert.Equal(0, Stock.Affordable(new Dictionary<string, int>(), new Dictionary<string, int>(), new Dictionary<string, int>()));
    }

    [Fact]
    public void APartNowhereToBeFoundMeansNone()
    {
        var cost = new Dictionary<string, int> { [Wood] = 1, [Nails] = 1 };

        Assert.Equal(0, Stock.Affordable(cost, new Dictionary<string, int> { [Wood] = 50 }, new Dictionary<string, int>()));
    }

    [Fact]
    public void ForARecipeNeedingAnyOneIngredientTheFirstThatIsFullyThereIsChosen()
    {
        var plans = new[]
        {
            Plan(Inventory(), false, [Bring(Wood, 10)], Chest(1, 5, Stack(Wood, 4))),
            Plan(Inventory(), false, [Bring(Stone, 10)], Chest(1, 5, Stack(Stone, 50))),
            Plan(Inventory(), false, [Bring(Nails, 10)], Chest(1, 5, Stack(Nails, 50))),
        };

        Assert.Equal(1, Choice.Best(plans));
    }

    [Fact]
    public void ForARecipeNeedingAnyOneIngredientWithNoneFullyThereTheClosestIsChosen()
    {
        var plans = new[]
        {
            Plan(Inventory(), false, [Bring(Wood, 10)], Chest(1, 5, Stack(Wood, 4))),
            Plan(Inventory(), false, [Bring(Stone, 10)], Chest(1, 5, Stack(Stone, 7))),
        };

        Assert.Equal(1, Choice.Best(plans));
    }

    [Fact]
    public void ForARecipeNeedingAnyOneIngredientOneAlreadyCarriedWins()
    {
        var plans = new[]
        {
            Plan(Inventory(), false, [MakeSureICarry(Wood, 10)], Chest(1, 5, Stack(Wood, 50))),
            Plan(Inventory(Stack(Stone, 10)), false, [MakeSureICarry(Stone, 10)], Chest(1, 5, Stack(Stone, 50))),
        };

        Assert.Equal(1, Choice.Best(plans));
    }
}
