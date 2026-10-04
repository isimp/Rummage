using System.Collections.Generic;
using Rummage.Core;
using Xunit;
using static Rummage.Tests.Fixtures;

namespace Rummage.Tests;

/// <summary>
/// Each press of the key brings enough for one more than what is carried already pays for. A part
/// the chests cannot give any more of does not hold the count down, so presses keep counting up.
/// </summary>
public class RepeatTests
{
    private static readonly Dictionary<string, int> Wall = new() { [Wood] = 10, [Stone] = 4 };

    private static readonly Dictionary<string, int> Plenty = new() { [Wood] = 999, [Stone] = 999 };

    private static int Next(Dictionary<string, int> carried, int step = 1, bool anyOf = false,
        Dictionary<string, int> cost = null, Dictionary<string, int> takeable = null)
        => Repeat.Next(cost ?? Wall, carried, takeable ?? Plenty, step, anyOf);

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
    public void TheScarcestPartTheChestsCanStillGiveDecidesWhatIsCarried()
    {
        Assert.Equal(1, Next(new() { [Wood] = 100 }));
    }

    [Fact]
    public void APartTheChestsCannotGiveDoesNotHoldTheCountDown()
    {
        var takeable = new Dictionary<string, int> { [Wood] = 999 };

        Assert.Equal(2, Next(new() { [Wood] = 10 }, takeable: takeable));
        Assert.Equal(3, Next(new() { [Wood] = 20 }, takeable: takeable));
    }

    [Fact]
    public void APartCarriedInFullButGoneFromTheChestsDoesNotHoldItDownEither()
    {
        var takeable = new Dictionary<string, int> { [Stone] = 999 };

        Assert.Equal(3, Next(new() { [Wood] = 10, [Stone] = 8 }, takeable: takeable));
    }

    [Fact]
    public void WhenTheChestsCanGiveNothingMoreTheCountStays()
    {
        Assert.Equal(2, Next(new() { [Wood] = 15, [Stone] = 4 }, takeable: []));
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
        Assert.Equal(3, Next(new() { [Stone] = 8 }, anyOf: true));
    }

    [Fact]
    public void SomethingThatCostsNothingIsOneStep()
    {
        Assert.Equal(1, Next(new() { [Wood] = 100 }, cost: []));
    }
}
