using System.Linq;
using Rummage.Core;
using Xunit;
using static Rummage.Tests.Fixtures;

namespace Rummage.Tests;

/// <summary>Filling one carried stack to a full stack from the chests.</summary>
public class TopUpTests
{
    private const string Arrows = "$item_arrow_fire";

    private static Want TopUp(ItemStack held) => Want.TopUp(held);

    [Fact]
    public void TheStackUnderTheCursorIsFilledToAFullStack()
    {
        var held = Stack(Arrows, 23, x: 4, y: 2, maxStack: 100);
        var bag = Inventory(held);
        var plan = Planner.Make([TopUp(held)], [Chest(1, 5, Stack(Arrows, 300, maxStack: 1000))], bag, false);

        Assert.Equal((Arrows, 100), plan.After(bag)[(4, 2)]);
        Assert.Equal(77, plan.Planned(Arrows));
    }

    [Fact]
    public void NothingSpillsIntoOtherSlots()
    {
        var held = Stack(Arrows, 23, x: 4, y: 2, maxStack: 100);
        var bag = Inventory(held, Stack(Arrows, 50, x: 0, y: 0, maxStack: 100));
        var plan = Planner.Make([TopUp(held)], [Chest(1, 5, Stack(Arrows, 500, maxStack: 1000))], bag, false);

        Assert.All(plan.Moves, m => Assert.Equal((4, 2), (m.ToX, m.ToY)));
        Assert.Equal(77, plan.Planned(Arrows));
    }

    [Fact]
    public void SeveralChestStacksTogetherFillIt()
    {
        var held = Stack(Arrows, 10, x: 1, maxStack: 100);
        var bag = Inventory(held);
        var plan = Planner.Make([TopUp(held)],
        [
            Chest(1, 5, Stack(Arrows, 30, maxStack: 100)),
            Chest(2, 6, Stack(Arrows, 30, maxStack: 100), Stack(Arrows, 70, x: 1, maxStack: 100)),
        ], bag, false);

        Assert.Equal((Arrows, 100), plan.After(bag)[(1, 0)]);
    }

    [Fact]
    public void OnlyTheVerySameKindIsUsed()
    {
        var held = Stack(Arrows, 23, maxStack: 100, worldLevel: 1);
        var bag = Inventory(held);
        var plan = Planner.Make([TopUp(held)],
            [Chest(1, 5, Stack(Arrows, 300, maxStack: 1000, worldLevel: 0), Stack(Arrows, 5, x: 1, maxStack: 1000, worldLevel: 2), Stack(Arrows, 40, x: 2, maxStack: 1000, worldLevel: 1))],
            bag, false);

        Assert.All(plan.Moves, m => Assert.Equal(2, m.FromX));
        Assert.Equal(40, plan.Planned(Arrows));
    }

    [Fact]
    public void AFullStackNeedsNothing()
    {
        var held = Stack(Arrows, 100, maxStack: 100);
        var plan = Planner.Make([TopUp(held)], [Chest(1, 5, Stack(Arrows, 300, maxStack: 1000))], Inventory(held), false);

        Assert.Empty(plan.Moves);
        Assert.Equal(0, plan.For(Arrows).Need);
    }

    [Fact]
    public void ASlotKeptForSomethingElseIsFilledWhenItIsTheStackUnderTheCursor()
    {
        // An ammo slot below the inventory grid holds the arrows.
        var held = Stack(Arrows, 23, x: 0, y: 4, maxStack: 100);
        var bag = new Bag(8, 5, [held], isUsable: (x, y) => y < 4);
        var plan = Planner.Make([TopUp(held)], [Chest(1, 5, Stack(Arrows, 300, maxStack: 1000))], bag, false);

        Assert.Equal((Arrows, 100), plan.After(bag)[(0, 4)]);
    }

    [Fact]
    public void LeaveOneStillHolds()
    {
        var held = Stack(Arrows, 90, maxStack: 100);
        var plan = Planner.Make([TopUp(held)], [Chest(1, 5, Stack(Arrows, 4, maxStack: 1000))], Inventory(held), true);

        Assert.Equal(3, plan.Planned(Arrows));
        Assert.Equal(1, plan.For(Arrows).KeptByLeaveOne);
    }

    [Fact]
    public void WhenAnotherPlayersChestAnswersFirstTheRestWaitsForTheNextPress()
    {
        var held = Stack(Arrows, 10, maxStack: 100);
        var bag = Inventory(held);
        var plan = Planner.Make([TopUp(held)],
        [
            Chest(1, 5, Reach.Routed, Stack(Arrows, 30, maxStack: 100)),
            Chest(2, 6, Stack(Arrows, 100, maxStack: 100)),
        ], bag, false);

        Assert.Single(plan.Moves);
        Assert.Equal(30, plan.Planned(Arrows));
        Assert.True(plan.For(Arrows).AwaitsAnswer);
        Assert.False(plan.For(Arrows).OutOfRoom);
    }

    [Fact]
    public void TheReportSaysToPressAgainForTheRest()
    {
        var held = Stack(Arrows, 10, maxStack: 100);
        var plan = Planner.Make([TopUp(held)],
            [Chest(1, 5, Reach.Routed, Stack(Arrows, 30, maxStack: 100)), Chest(2, 6, Stack(Arrows, 100, maxStack: 100))],
            Inventory(held), false);

        var lines = Report.Lines("Fire arrow", plan, new System.Collections.Generic.Dictionary<string, int> { [Arrows] = 30 }, 0, n => "Fire arrow");

        Assert.Equal(["Fire arrow: took 30 Fire arrow", "60 Fire arrow short: the rest once the other chest has answered; press again"], lines);
    }
}
