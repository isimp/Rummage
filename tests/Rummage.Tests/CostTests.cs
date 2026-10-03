using Rummage.Core;
using Xunit;
using static Rummage.Tests.Fixtures;

namespace Rummage.Tests;

/// <summary>What one of something costs, as recipes and pieces list it.</summary>
public class CostTests
{
    [Fact]
    public void AnIngredientListedTwiceCountsWithBothAmounts()
    {
        var cost = Cost.Sum([(Wood, 4), (Stone, 2), (Wood, 6)]);

        Assert.Equal(10, cost[Wood]);
        Assert.Equal(2, cost[Stone]);
        Assert.Equal(2, cost.Count);
    }

    [Fact]
    public void NothingListedCostsNothing()
    {
        Assert.Empty(Cost.Sum([]));
    }

    [Fact]
    public void AnAmountOfNothingIsLeftOut()
    {
        Assert.False(Cost.Sum([(Wood, 0)]).ContainsKey(Wood));
    }
}
