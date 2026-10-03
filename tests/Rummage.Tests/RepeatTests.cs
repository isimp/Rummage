using System.Collections.Generic;
using Rummage.Core;
using Xunit;
using static Rummage.Tests.Fixtures;

namespace Rummage.Tests;

/// <summary>Each press of the key brings enough for one more than what is carried already pays for.</summary>
public class RepeatTests
{
    private static readonly Dictionary<string, int> Wall = new() { [Wood] = 10, [Stone] = 4 };

    private static int Next(Dictionary<string, int> carried, int step = 1, bool anyOf = false, Dictionary<string, int> cost = null)
        => Repeat.Next(cost ?? Wall, carried, step, anyOf);

    [Fact]
    public void TheFirstPressBringsOne()
    {
        Assert.Equal(1, Next([]));
    }

    [Fact]
    public void WithOneAlreadyCarriedThePressBringsASecond()
    {
        Assert.Equal(2, Next(new() { [Wood] = 10, [Stone] = 4 }));
    }

    [Fact]
    public void WithFiveCarriedThePressBringsASixth()
    {
        Assert.Equal(6, Next(new() { [Wood] = 50, [Stone] = 20 }));
    }

    [Fact]
    public void AHalfPaidOneIsCompletedFirst()
    {
        Assert.Equal(2, Next(new() { [Wood] = 15, [Stone] = 20 }));
    }

    [Fact]
    public void TheScarcestPartDecidesWhatIsCarried()
    {
        Assert.Equal(1, Next(new() { [Wood] = 100 }));
    }

    [Fact]
    public void ARecipeStepsByTheCraftAmount()
    {
        Assert.Equal(5, Next([], step: 5));
        Assert.Equal(10, Next(new() { [Wood] = 50, [Stone] = 20 }, step: 5));
        Assert.Equal(10, Next(new() { [Wood] = 70, [Stone] = 28 }, step: 5));
    }

    [Fact]
    public void ForAnyOneIngredientTheBestCoveredOneCounts()
    {
        var cost = new Dictionary<string, int> { [Wood] = 10, [Stone] = 4 };

        Assert.Equal(3, Next(new() { [Stone] = 8 }, anyOf: true, cost: cost));
    }

    [Fact]
    public void SomethingThatCostsNothingIsOneStep()
    {
        Assert.Equal(1, Next(new() { [Wood] = 100 }, cost: []));
    }
}
