using System.Linq;
using Rummage.Core;
using Xunit;
using static Rummage.Tests.Fixtures;

namespace Rummage.Tests;

/// <summary>What a grab takes, from where, and where it lands.</summary>
public class PlannerTests
{
    [Fact]
    public void YouGetExactlyWhatYouAskedFor()
    {
        var plan = Plan(Inventory(), false, [Bring(Wood, 30)], Chest(1, 5, Stack(Wood, 50)));

        Assert.Equal(30, plan.Planned(Wood));
        Assert.Equal(0, plan.For(Wood).Short);
    }

    [Fact]
    public void TheNearestChestIsEmptiedFirst()
    {
        var plan = Plan(Inventory(), false, [Bring(Wood, 30)],
            Chest(1, 12, Stack(Wood, 50)),
            Chest(2, 3, Stack(Wood, 20)));

        Assert.Equal(20, plan.TakenFrom(2));
        Assert.Equal(10, plan.TakenFrom(1));
    }

    [Fact]
    public void WhatYouAlreadyCarryIsNotFetchedAgainWhenYouAskForATotal()
    {
        var plan = Plan(Inventory(Stack(Wood, 7)), false, [MakeSureICarry(Wood, 10)], Chest(1, 5, Stack(Wood, 50)));

        Assert.Equal(3, plan.Planned(Wood));
        Assert.Equal(7, plan.For(Wood).Carried);
    }

    [Fact]
    public void AskingForATotalYouAlreadyCarryTakesNothing()
    {
        var plan = Plan(Inventory(Stack(Wood, 12)), false, [MakeSureICarry(Wood, 10)], Chest(1, 5, Stack(Wood, 50)));

        Assert.Empty(plan.Moves);
        Assert.Equal(0, plan.For(Wood).Short);
    }

    [Fact]
    public void WithLeaveOneEveryChestKeepsOneOfEachItemItHolds()
    {
        var plan = Plan(Inventory(), true, [Bring(Wood, 100), Bring(Stone, 100)],
            Chest(1, 5, Stack(Wood, 20), Stack(Stone, 4, x: 1)),
            Chest(2, 8, Stack(Wood, 5)));

        Assert.Equal(19 + 4, plan.Planned(Wood));
        Assert.Equal(3, plan.Planned(Stone));
        Assert.Equal(2, plan.For(Wood).KeptByLeaveOne);
        Assert.Equal(1, plan.For(Stone).KeptByLeaveOne);
    }

    [Fact]
    public void WithLeaveOneTheUnitKeptIsCountedOncePerChestNotPerStack()
    {
        var plan = Plan(Inventory(), true, [Bring(Wood, 200)],
            Chest(1, 5, Stack(Wood, 50), Stack(Wood, 50, x: 1), Stack(Wood, 3, x: 2)));

        Assert.Equal(102, plan.Planned(Wood));
    }

    [Fact]
    public void WithLeaveOneAChestHoldingASingleUnitGivesNothing()
    {
        var plan = Plan(Inventory(), true, [Bring(Wood, 5)], Chest(1, 5, Stack(Wood, 1)));

        Assert.Empty(plan.Moves);
        Assert.Equal(5, plan.For(Wood).Short);
        Assert.Equal(1, plan.For(Wood).KeptByLeaveOne);
    }

    [Fact]
    public void WithoutLeaveOneAChestMayBeEmptied()
    {
        var plan = Plan(Inventory(), false, [Bring(Wood, 100)], Chest(1, 5, Stack(Wood, 20)));

        Assert.Equal(20, plan.Planned(Wood));
        Assert.Equal(0, plan.For(Wood).KeptByLeaveOne);
    }

    [Fact]
    public void ItemsAChestsRulesForbidAreNeverTakenAndTheShortfallSaysSo()
    {
        var plan = Plan(Inventory(), false, [Bring(Wood, 10), Bring(Stone, 10)],
            Chest(1, 5, prefab => prefab != "Wood", Stack(Wood, 50), Stack(Stone, 50, x: 1)));

        Assert.Equal(0, plan.Planned(Wood));
        Assert.Equal(10, plan.Planned(Stone));
        Assert.Equal(50, plan.For(Wood).NotAllowed);
        Assert.Equal(10, plan.For(Wood).Short);
    }

    [Fact]
    public void AChestNobodyCanSaveForYouIsNeverTouched()
    {
        var plan = Plan(Inventory(), false, [Bring(Wood, 10)],
            Chest(1, 2, Reach.Closed, Stack(Wood, 50)),
            Chest(2, 9, Stack(Wood, 4)));

        Assert.Equal(0, plan.TakenFrom(1));
        Assert.Equal(4, plan.TakenFrom(2));
        Assert.Equal(50, plan.For(Wood).Unreachable);
        Assert.Equal(6, plan.For(Wood).Short);
    }

    [Fact]
    public void AChestAnotherPlayerOwnsIsUsedThroughTheirGame()
    {
        var plan = Plan(Inventory(), false, [Bring(Wood, 10)], Chest(1, 2, Reach.Routed, Stack(Wood, 50)));

        Assert.Equal(10, plan.TakenFrom(1));
        Assert.All(plan.Moves, m => Assert.Equal(Reach.Routed, m.Reach));
    }

    [Fact]
    public void WhenChestsHoldTooLittleYouGetWhatThereIsAndHearWhatIsMissing()
    {
        var plan = Plan(Inventory(), false, [Bring(Wood, 80)], Chest(1, 5, Stack(Wood, 30)));

        Assert.Equal(30, plan.Planned(Wood));
        Assert.Equal(50, plan.For(Wood).Short);
        Assert.False(plan.For(Wood).OutOfRoom);
    }

    [Fact]
    public void NothingIsPlannedThatDoesNotFitYourInventory()
    {
        // One free slot left; wood stacks to 50.
        var bag = Inventory(2, 1, Stack(Stone, 50, x: 0));
        var plan = Plan(bag, false, [Bring(Wood, 80)], Chest(1, 5, Stack(Wood, 80)));

        Assert.Equal(50, plan.Planned(Wood));
        Assert.Equal(30, plan.For(Wood).Short);
        Assert.True(plan.For(Wood).OutOfRoom);
    }

    [Fact]
    public void ACarriedPartStackIsToppedUpBeforeANewSlotIsUsed()
    {
        var bag = Inventory(Stack(Wood, 45, x: 3, y: 2));
        var plan = Plan(bag, false, [Bring(Wood, 10)], Chest(1, 5, Stack(Wood, 50)));

        var after = plan.After(bag);
        Assert.Equal((Wood, 50), after[(3, 2)]);
        Assert.Equal(2, after.Count);
        Assert.Equal(5, after.Values.Single(v => v.Count != 50).Count);
    }

    [Fact]
    public void AFullInventoryWithOnlyAPartStackTakesJustWhatTheStackHoldsMore()
    {
        // The case that duplicated items before: one stack with room, no free slot.
        var bag = Inventory(1, 1, Stack(Wood, 45));
        var plan = Plan(bag, false, [Bring(Wood, 10)], Chest(1, 5, Stack(Wood, 50)));

        Assert.Equal(5, plan.Planned(Wood));
        Assert.True(plan.For(Wood).OutOfRoom);
    }

    [Fact]
    public void EveryMoveFitsItsTargetSlotWhole()
    {
        var bag = Inventory(Stack(Wood, 30), Stack(Wood, 48, x: 1));
        var plan = Plan(bag, false, [Bring(Wood, 140)],
            Chest(1, 5, Stack(Wood, 50), Stack(Wood, 50, x: 1), Stack(Wood, 50, x: 2)));

        Assert.All(plan.After(bag).Values, slot => Assert.InRange(slot.Count, 1, 50));
        Assert.Equal(140, plan.Planned(Wood));
    }

    [Fact]
    public void ASlotWaitingForAnotherPlayersChestGetsNoSecondTakeInTheSameGrab()
    {
        var bag = Inventory(Stack(Wood, 10));
        var plan = Plan(bag, false, [Bring(Wood, 60)],
            Chest(1, 5, Reach.Routed, Stack(Wood, 15)),
            Chest(2, 6, Stack(Wood, 15)),
            Chest(3, 7, Reach.Routed, Stack(Wood, 30)));

        // Moves are made in order; once a slot waits for an answer, no later move may target it.
        for (int i = 0; i < plan.Moves.Count; i++)
        {
            var routed = plan.Moves[i];
            if (routed.Reach == Reach.Routed)
            {
                Assert.DoesNotContain(plan.Moves.Skip(i + 1), m => (m.ToX, m.ToY) == (routed.ToX, routed.ToY));
            }
        }

        Assert.Equal(60, plan.Planned(Wood));
    }

    [Fact]
    public void TakesFromChestsThisGameHoldsMayFillOneStackTogether()
    {
        var bag = Inventory(Stack(Wood, 10));
        var plan = Plan(bag, false, [Bring(Wood, 40)],
            Chest(1, 5, Stack(Wood, 15)),
            Chest(2, 6, Stack(Wood, 25)));

        Assert.Equal((Wood, 50), plan.After(bag)[(0, 0)]);
        Assert.Single(plan.After(bag));
    }

    [Fact]
    public void NoChestStackGivesMoreThanItHolds()
    {
        var bag = Inventory(Stack(Wood, 49), Stack(Wood, 49, x: 1), Stack(Wood, 49, x: 2));
        var plan = Plan(bag, false, [Bring(Wood, 100)], Chest(1, 5, Stack(Wood, 40, x: 4, y: 1)));

        Assert.Equal(40, plan.Moves.Where(m => m.FromX == 4 && m.FromY == 1).Sum(m => m.Amount));
    }

    [Fact]
    public void SlotsTheInventoryKeepsForSomethingElseAreNeverFilled()
    {
        // The bottom row stands for equipment or quick slots another mod adds.
        var bag = new Bag(2, 2, [], isUsable: (x, y) => y == 0);
        var plan = Plan(bag, false, [Bring(Wood, 200)], Chest(1, 5, Stack(Wood, 50), Stack(Wood, 50, x: 1), Stack(Wood, 50, x: 2), Stack(Wood, 50, x: 3)));

        Assert.All(plan.Moves, m => Assert.Equal(0, m.ToY));
        Assert.Equal(100, plan.Planned(Wood));
        Assert.True(plan.For(Wood).OutOfRoom);
    }

    [Fact]
    public void ItemsBelowTheWorldLevelAreNeitherCountedNorFetched()
    {
        var bag = Inventory(Stack(Wood, 10, worldLevel: 0));
        var want = new Want(Wood, 10, countCarried: true, minWorldLevel: 1);
        var plan = Planner.Make([want], [Chest(1, 5, Stack(Wood, 50, worldLevel: 0), Stack(Wood, 4, x: 1, worldLevel: 2))], bag, false);

        Assert.Equal(0, plan.For(Wood).Carried);
        Assert.Equal(4, plan.Planned(Wood));
        Assert.Equal(6, plan.For(Wood).Short);
    }

    [Fact]
    public void StacksOfDifferentWorldLevelsNeverShareASlot()
    {
        var bag = Inventory(1, 2, Stack(Wood, 10, worldLevel: 0));
        var plan = Plan(bag, false, [Bring(Wood, 5)], Chest(1, 5, Stack(Wood, 5, worldLevel: 2)));

        Assert.All(plan.Moves, m => Assert.Equal((0, 1), (m.ToX, m.ToY)));
    }

    [Fact]
    public void QualitiesOfAnItemWithQualityLevelsNeverShareASlot()
    {
        var bag = Inventory(1, 2, Stack(Sword, 1, maxStack: 2, quality: 1, qualityMatters: true));
        var plan = Plan(bag, false, [Bring(Sword, 1)], Chest(1, 5, Stack(Sword, 1, maxStack: 2, quality: 3, qualityMatters: true)));

        Assert.All(plan.Moves, m => Assert.Equal((0, 1), (m.ToX, m.ToY)));
    }

    [Fact]
    public void SeveralItemsShareTheFreeSlotsWithoutOverlapping()
    {
        var bag = Inventory(3, 1);
        var plan = Plan(bag, false, [Bring(Wood, 60), Bring(Stone, 60)],
            Chest(1, 5, Stack(Wood, 100, maxStack: 50), Stack(Stone, 100, x: 1, maxStack: 50)));

        Assert.Equal(3, plan.Moves.Count);
        Assert.Equal(3, plan.Moves.Select(m => (m.ToX, m.ToY)).Distinct().Count());
        Assert.Equal(60, plan.Planned(Wood));
        Assert.Equal(50, plan.Planned(Stone));
        Assert.True(plan.For(Stone).OutOfRoom);
    }

    [Fact]
    public void AskingForTheSameItemTwiceAddsUp()
    {
        var plan = Plan(Inventory(), false, [Bring(Nails, 4), Bring(Nails, 6)], Chest(1, 5, Stack(Nails, 50, maxStack: 100)));

        Assert.Equal(10, plan.Planned(Nails));
        Assert.Single(plan.Outcomes);
    }

    [Fact]
    public void AChestStackStaysWhereItIsWhenNothingIsTakenFromIt()
    {
        var plan = Plan(Inventory(), true, [Bring(Wood, 3)], Chest(1, 5, Stack(Wood, 50), Stack(Wood, 50, x: 1)));

        Assert.Single(plan.Moves);
        Assert.Equal(3, plan.Planned(Wood));
    }

    [Fact]
    public void AskingForNothingTakesNothing()
    {
        var plan = Plan(Inventory(), false, [Bring(Wood, 0)], Chest(1, 5, Stack(Wood, 50)));

        Assert.Empty(plan.Moves);
    }
}
