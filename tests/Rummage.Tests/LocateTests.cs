using System.Collections.Generic;
using Rummage.Core;
using Xunit;
using static Rummage.Tests.Fixtures;

namespace Rummage.Tests;

/// <summary>What finding an item says: how many, in how many chests, and how far the nearest is.</summary>
public class LocateTests
{
    private static string Shown(string name) => name switch
    {
        Wood => "Wood",
        Stone => "Stone",
        Nails => "Bronze nails",
        _ => name,
    };

    private static (string, IReadOnlyList<(float, int)>) Part(string name, params (float Distance, int Count)[] chests) => (name, chests);

    [Fact]
    public void AnItemIsCountedOverItsChestsWithTheNearestOne()
    {
        var lines = Locate.Lines([Part(Wood, (12.4f, 50), (6.2f, 30), (40f, 40))], 50f, Shown);

        Assert.Equal(["Wood: 120 in 3 chests, nearest 6 m"], lines);
    }

    [Fact]
    public void OneChestIsOneChest()
    {
        Assert.Equal(["Stone: 4 in 1 chest, nearest 3 m"], Locate.Lines([Part(Stone, (3f, 4))], 50f, Shown));
    }

    [Fact]
    public void AnItemInNoChestSaysHowFarWasLooked()
    {
        Assert.Equal(["Bronze nails: in no chest within 50 m"], Locate.Lines([Part(Nails)], 50f, Shown));
    }

    [Fact]
    public void APieceOrRecipeGetsALinePerPart()
    {
        var lines = Locate.Lines([Part(Wood, (6f, 30)), Part(Nails)], 50f, Shown);

        Assert.Equal(["Wood: 30 in 1 chest, nearest 6 m", "Bronze nails: in no chest within 50 m"], lines);
    }

    [Fact]
    public void ChestsHoldingNoneOfItAreNotCounted()
    {
        Assert.Equal(["Wood: 5 in 1 chest, nearest 9 m"], Locate.Lines([Part(Wood, (2f, 0), (9f, 5))], 50f, Shown));
    }
}
