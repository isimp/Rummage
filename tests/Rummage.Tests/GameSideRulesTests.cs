using System.Collections.Generic;
using System.Linq;
using Rummage.Core;
using Xunit;
using static Rummage.Tests.Fixtures;

namespace Rummage.Tests;

/// <summary>A piece the tests light: a chest's building piece.</summary>
public sealed class Piece
{
    public bool Destroyed { get; set; }
}

/// <summary>
/// Chests stay lit for as long as a caller asks, with the game's highlight, which fades after a
/// fifth of a second: so a lit piece is lit again every tenth of a second until its time is up or
/// it is gone. Lighting a lit piece again keeps it lit for the longer of the two.
/// </summary>
public class LightsTests
{
    private static Lights<Piece> Lights() => new Lights<Piece>(piece => piece.Destroyed);

    [Fact]
    public void ALitPieceIsLitAgainEveryTenthOfASecondUntilItsTimeIsUp()
    {
        var lights = Lights();
        var chest = new Piece();
        lights.Light(chest, until: 12f);

        Assert.Equal(new[] { chest }, lights.Due(10f));
        Assert.Empty(lights.Due(10.05f));
        Assert.Equal(new[] { chest }, lights.Due(10.1f));
        Assert.Empty(lights.Due(12f));
        Assert.Equal(0, lights.Count);
    }

    [Fact]
    public void LightingAgainKeepsThePieceLitForTheLongerTime()
    {
        var lights = Lights();
        var chest = new Piece();
        lights.Light(chest, until: 15f);
        lights.Light(chest, until: 12f);

        Assert.Equal(new[] { chest }, lights.Due(13f));
    }

    [Fact]
    public void APieceThatIsGoneIsNoLongerLit()
    {
        var lights = Lights();
        var chest = new Piece();
        lights.Light(chest, until: 15f);

        chest.Destroyed = true;

        Assert.Empty(lights.Due(10f));
        Assert.Equal(0, lights.Count);
    }

    [Fact]
    public void WithNothingLitThereIsNothingToDo()
    {
        Assert.Empty(Lights().Due(10f));
    }
}

/// <summary>
/// Rummage's lines go to the corner as the game's own messages, or, while the inventory is open
/// and covers the corner, together as one message in the middle of the screen.
/// </summary>
public class PlacementTests
{
    [Fact]
    public void WithTheInventoryClosedEachLineGoesToTheCorner()
    {
        var (centre, corner) = Placement.Of(inventoryOpen: false, new[] { "Took 20 Wood", "Short 3 Stone" });

        Assert.Null(centre);
        Assert.Equal(new[] { "Took 20 Wood", "Short 3 Stone" }, corner);
    }

    [Fact]
    public void WithTheInventoryOpenTheLinesGoTogetherToTheMiddle()
    {
        var (centre, corner) = Placement.Of(inventoryOpen: true, new[] { "Took 20 Wood", "Short 3 Stone" });

        Assert.Equal("Took 20 Wood\nShort 3 Stone", centre);
        Assert.Empty(corner);
    }

    [Fact]
    public void NoLinesShowNothing()
    {
        var (centre, corner) = Placement.Of(inventoryOpen: true, new string[0]);
        Assert.Null(centre);
        Assert.Empty(corner);

        (centre, corner) = Placement.Of(inventoryOpen: false, null);
        Assert.Null(centre);
        Assert.Empty(corner);
    }
}

/// <summary>
/// Whether the player could open a chest by hand, asked as the game asks: the ward covering it,
/// where the chest checks wards, and its privacy: a public chest anyone, a private one only its
/// builder, any other setting nobody. A chest in reach that cannot be used says why: someone else
/// has it open or pulls its cart, it is someone else's private chest, or it is kept out otherwise.
/// A chest this game holds is taken from directly, one another game holds only through
/// MultiUserChest, and without it not at all. Only items made for the world's level count as
/// carried.
/// </summary>
public class ChestRulesTests
{
    [Fact]
    public void AWardThePlayerIsNotOnKeepsThemOutOfAChestThatChecksWards()
    {
        Assert.False(ChestRules.MayOpen(checksWard: true, wardLetsIn: false, ChestPrivacy.Public, playerBuiltIt: true));
        Assert.True(ChestRules.MayOpen(checksWard: true, wardLetsIn: true, ChestPrivacy.Public, playerBuiltIt: false));
    }

    [Fact]
    public void AChestThatDoesNotCheckWardsIgnoresThem()
    {
        Assert.True(ChestRules.MayOpen(checksWard: false, wardLetsIn: false, ChestPrivacy.Public, playerBuiltIt: false));
    }

    [Fact]
    public void APrivateChestOpensOnlyForItsBuilderAndAnyOtherSettingForNobody()
    {
        Assert.True(ChestRules.MayOpen(checksWard: false, wardLetsIn: true, ChestPrivacy.Private, playerBuiltIt: true));
        Assert.False(ChestRules.MayOpen(checksWard: false, wardLetsIn: true, ChestPrivacy.Private, playerBuiltIt: false));
        Assert.False(ChestRules.MayOpen(checksWard: false, wardLetsIn: true, ChestPrivacy.Other, playerBuiltIt: true));
    }

    [Fact]
    public void AChestSomeoneElseHasOpenOrWhoseCartIsPulledIsInUse()
    {
        Assert.Equal(Bar.InUse, ChestRules.BarOf(openElsewhere: true, cartPulled: false, ChestPrivacy.Public, playerBuiltIt: true));
        Assert.Equal(Bar.InUse, ChestRules.BarOf(openElsewhere: false, cartPulled: true, ChestPrivacy.Private, playerBuiltIt: false));
    }

    [Fact]
    public void SomeoneElsesPrivateChestIsPrivateAndAnyOtherIsKeptOut()
    {
        Assert.Equal(Bar.Private, ChestRules.BarOf(openElsewhere: false, cartPulled: false, ChestPrivacy.Private, playerBuiltIt: false));
        Assert.Equal(Bar.Locked, ChestRules.BarOf(openElsewhere: false, cartPulled: false, ChestPrivacy.Private, playerBuiltIt: true));
        Assert.Equal(Bar.Locked, ChestRules.BarOf(openElsewhere: false, cartPulled: false, ChestPrivacy.Public, playerBuiltIt: false));
    }

    [Fact]
    public void AChestThisGameHoldsIsTakenFromDirectly()
    {
        Assert.Equal(Reach.Owned, ChestRules.ReachOf(held: true, routed: true));
        Assert.Equal(Reach.Owned, ChestRules.ReachOf(held: true, routed: false));
    }

    [Fact]
    public void AChestAnotherGameHoldsIsTakenFromOnlyThroughMultiUserChest()
    {
        Assert.Equal(Reach.Routed, ChestRules.ReachOf(held: false, routed: true));
        Assert.Equal(Reach.Closed, ChestRules.ReachOf(held: false, routed: false));
    }

    [Fact]
    public void OnlyItemsMadeForTheWorldsLevelCountAsCarried()
    {
        Assert.True(ChestRules.Counted(itemWorldLevel: 2, worldLevel: 2));
        Assert.True(ChestRules.Counted(itemWorldLevel: 3, worldLevel: 2));
        Assert.False(ChestRules.Counted(itemWorldLevel: 1, worldLevel: 2));
    }
}

/// <summary>
/// What the search box lists of items: everything the chests in reach can give, everything the
/// chests within the find radius hold, so an item kept back can still be found, and what the box
/// last found or took, even once no chest holds it. What a chest in reach holds counts in full
/// even where the find radius saw less of it. Pieces and recipes are listed as their settings say.
/// </summary>
public class CatalogRulesTests
{
    [Fact]
    public void AnItemIsListedWhenHeldTakeableOrLastUsed()
    {
        var names = CatalogRules.ItemNames(held: new[] { Wood, Stone }, takeable: new[] { Stone, Nails }, lastUsed: new[] { Sword });

        Assert.Equal(new[] { Wood, Stone, Nails, Sword }, names);
    }

    [Fact]
    public void WhatAChestInReachHoldsCountsInFull()
    {
        var held = new Dictionary<string, int> { [Wood] = 10, [Stone] = 40 };

        CatalogRules.MergeReachable(held, new Dictionary<string, int> { [Wood] = 25, [Stone] = 5, [Nails] = 3 });

        Assert.Equal(25, held[Wood]);
        Assert.Equal(40, held[Stone]);
        Assert.Equal(3, held[Nails]);
    }

    [Fact]
    public void PiecesAndRecipesAreListedAsTheirSettingsSay()
    {
        Assert.True(CatalogRules.Lists(EntryKind.Piece, pieces: true, recipes: false));
        Assert.False(CatalogRules.Lists(EntryKind.Recipe, pieces: true, recipes: false));
        Assert.True(CatalogRules.Lists(EntryKind.Recipe, pieces: false, recipes: true));
        Assert.False(CatalogRules.Lists(EntryKind.Piece, pieces: false, recipes: true));
    }
}
