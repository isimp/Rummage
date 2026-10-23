using Rummage.Core;
using Xunit;

namespace Rummage.Tests;

/// <summary>A pin the tests put on the map.</summary>
public sealed class Pin
{
}

/// <summary>
/// A ping marks a chest on the map for a few seconds and is then taken down: one per chest, never
/// saved, and all of them let go when the map they are on goes.
/// </summary>
public class PingsTests
{
    [Fact]
    public void APingIsTakenDownOnceItsTimeIsUp()
    {
        var pings = new Pings<Piece, Pin>();
        var chest = new Piece();
        var pin = new Pin();
        pings.Put(chest, pin, until: 16f);

        Assert.Empty(pings.Due(15.9f));
        Assert.Same(pin, pings.PinOf(chest));
        Assert.Equal(new[] { pin }, pings.Due(16f));
        Assert.Empty(pings.Due(17f));
        Assert.Equal(0, pings.Count);
        Assert.Null(pings.PinOf(chest));
    }

    [Fact]
    public void PingingAChestAgainReplacesItsPing()
    {
        var pings = new Pings<Piece, Pin>();
        var chest = new Piece();
        var first = new Pin();
        var second = new Pin();
        pings.Put(chest, first, until: 16f);

        Assert.Same(first, pings.Put(chest, second, until: 20f));
        Assert.Equal(1, pings.Count);
        Assert.Same(second, pings.PinOf(chest));
        Assert.Empty(pings.Due(18f));
        Assert.Equal(new[] { second }, pings.Due(20f));
    }

    [Fact]
    public void PingingANewChestReplacesNothing()
    {
        var pings = new Pings<Piece, Pin>();
        pings.Put(new Piece(), new Pin(), until: 16f);

        Assert.Null(pings.Put(new Piece(), new Pin(), until: 16f));
        Assert.Equal(2, pings.Count);
    }

    [Fact]
    public void EveryPingIsLetGoWhenTheMapGoes()
    {
        var pings = new Pings<Piece, Pin>();
        var one = new Pin();
        var two = new Pin();
        pings.Put(new Piece(), one, until: 16f);
        pings.Put(new Piece(), two, until: 30f);

        Assert.Equal(new[] { one, two }, pings.TakeAll());
        Assert.Equal(0, pings.Count);
        Assert.Empty(pings.Due(40f));
    }

    [Fact]
    public void WithNothingPingedThereIsNothingToTakeDown()
    {
        Assert.Empty(new Pings<Piece, Pin>().Due(10f));
    }
}
