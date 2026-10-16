using System.Collections.Generic;
using System.Linq;
using Rummage.Core;
using Xunit;
using static Rummage.Tests.Fixtures;

namespace Rummage.Tests;

/// <summary>
/// What a press for a build piece asks the chests for under each <see cref="PiecePress"/> rule.
/// TopUp counts what the inventory holds towards the piece; FullSet brings the whole cost on every
/// press, so presses for different pieces add up. Recipes and items keep their own rules either way.
/// </summary>
public class PressTests
{
    private const string FineWood = "$item_finewood";
    private const string Resin = "$item_resin";

    private static readonly (string Name, int Amount)[] Stairs = [(FineWood, 3), (Resin, 2)];

    private static readonly Source Plenty = Chest(1, 5, Stack(FineWood, 50), Stack(Resin, 50, x: 1));

    /// <summary>The inventory after a portal press: 20 fine wood, nothing else.</summary>
    private static Bag AfterThePortal() => Inventory(Stack(FineWood, 20));

    private static Plan Press(EntryKind kind, PiecePress rule, Bag bag, int? typed = null, params Source[] chests)
    {
        var cost = Stairs.ToDictionary(p => p.Name, p => p.Amount);
        var carried = bag.Stacks.GroupBy(s => s.Name).ToDictionary(g => g.Key, g => g.Sum(s => s.Count));
        var takeable = new Dictionary<string, int> { [FineWood] = 999, [Resin] = 999 };
        int count = typed ?? Presses.Next(kind, rule, cost, carried, takeable, step: 1, anyOf: false);
        return Planner.Make(Presses.Wants(Stairs, count, kind, rule, worldLevel: 0), chests.Length > 0 ? chests : [Plenty], bag, leaveOne: false);
    }

    [Fact]
    public void WithFullSetThePieceAfterAPortalStillBringsItsOwnFineWood()
    {
        var plan = Press(EntryKind.Piece, PiecePress.FullSet, AfterThePortal());

        Assert.Equal(3, plan.Planned(FineWood));
        Assert.Equal(2, plan.Planned(Resin));
    }

    [Fact]
    public void WithTopUpTheFineWoodCarriedForThePortalCoversThePiece()
    {
        var plan = Press(EntryKind.Piece, PiecePress.TopUp, AfterThePortal());

        Assert.Equal(0, plan.Planned(FineWood));
        Assert.Equal(2, plan.Planned(Resin));
    }

    [Fact]
    public void WithFullSetEveryPressIsForOneMoreWhateverIsCarried()
    {
        var cost = new Dictionary<string, int> { [FineWood] = 3 };
        var carried = new Dictionary<string, int> { [FineWood] = 300 };
        var takeable = new Dictionary<string, int> { [FineWood] = 999 };

        Assert.Equal(1, Presses.Next(EntryKind.Piece, PiecePress.FullSet, cost, carried, takeable, step: 1, anyOf: false));
        Assert.Equal(101, Presses.Next(EntryKind.Piece, PiecePress.TopUp, cost, carried, takeable, step: 1, anyOf: false));
    }

    [Fact]
    public void WithFullSetACountTypedInTheSearchBoxBringsThatManyWholeSets()
    {
        var plan = Press(EntryKind.Piece, PiecePress.FullSet, AfterThePortal(), typed: 3);

        Assert.Equal(9, plan.Planned(FineWood));
        Assert.Equal(6, plan.Planned(Resin));
    }

    [Fact]
    public void WithTopUpACountTypedInTheSearchBoxIsATotalToCarry()
    {
        var plan = Press(EntryKind.Piece, PiecePress.TopUp, AfterThePortal(), typed: 3);

        Assert.Equal(0, plan.Planned(FineWood));
        Assert.Equal(6, plan.Planned(Resin));
    }

    [Fact]
    public void RecipesStillCountWhatIsCarriedUnderFullSet()
    {
        var plan = Press(EntryKind.Recipe, PiecePress.FullSet, AfterThePortal());

        Assert.Equal(0, plan.Planned(FineWood));
        Assert.Equal(2, plan.Planned(Resin));
    }

    [Fact]
    public void AnItemIsANumberToBringUnderEitherRule()
    {
        foreach (var rule in new[] { PiecePress.TopUp, PiecePress.FullSet })
        {
            var wants = Presses.Wants([(FineWood, 1)], 5, EntryKind.Item, rule, worldLevel: 0);
            var plan = Planner.Make(wants, [Plenty], AfterThePortal(), leaveOne: false);

            Assert.Equal(5, plan.Planned(FineWood));
        }
    }

    [Fact]
    public void WithFullSetWhatTheChestsLackIsStillNamedShort()
    {
        var plan = Press(EntryKind.Piece, PiecePress.FullSet, AfterThePortal(), chests: Chest(1, 5, Stack(FineWood, 1)));

        Assert.Equal(1, plan.Planned(FineWood));
        Assert.Equal(2, plan.For(FineWood).Short);
        Assert.Equal(2, plan.For(Resin).Short);
    }
}
