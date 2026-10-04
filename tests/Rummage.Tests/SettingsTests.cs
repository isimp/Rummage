using System.Collections.Generic;
using Rummage.Core;
using Xunit;
using static Rummage.Tests.Fixtures;

namespace Rummage.Tests;

/// <summary>What the settings change in what Rummage takes and tells.</summary>
public class SettingsTests
{
    private const string Arrows = "$item_arrow_fire";

    private static string Shown(string name) => name switch
    {
        Wood => "Wood",
        Stone => "Stone",
        _ => name,
    };

    private static IReadOnlyList<string> Lines(Plan plan, Messages messages, int changed = 0)
    {
        var took = new Dictionary<string, int>();
        foreach (var move in plan.Moves)
        {
            took[move.Name] = took.GetValueOrDefault(move.Name) + move.Amount;
        }

        return Report.Lines("Workbench", plan, new Done(took, new Dictionary<string, int>(), changed, refused: 0), Shown, true, messages);
    }

    [Fact]
    public void ATopUpAmountLimitsWhatOnePressTakes()
    {
        var held = Stack(Arrows, 23, x: 4, y: 2, maxStack: 100);
        var bag = Inventory(held);
        var plan = Planner.Make([Want.TopUp(held, atMost: 20)], [Chest(1, 5, Stack(Arrows, 300, maxStack: 1000))], bag, false);

        Assert.Equal(20, plan.Planned(Arrows));
        Assert.Equal((Arrows, 43), plan.After(bag)[(4, 2)]);
    }

    [Fact]
    public void ATopUpAmountLargerThanTheRoomLeftStillOnlyFillsTheStack()
    {
        var held = Stack(Arrows, 90, maxStack: 100);

        Assert.Equal(10, Want.TopUp(held, atMost: 20).Count);
    }

    [Fact]
    public void ATopUpAmountOfZeroFillsTheStack()
    {
        var held = Stack(Arrows, 23, maxStack: 100);

        Assert.Equal(77, Want.TopUp(held, atMost: 0).Count);
    }

    [Fact]
    public void ShortMessagesNameWhatIsShortWithoutTheReasons()
    {
        var plan = Plan(Inventory(), false, [Bring(Wood, 10), Bring(Stone, 4)], Chest(1, 5, Stack(Wood, 5)));

        Assert.Equal(["Workbench: took 5 Wood", "Short: 5 Wood, 4 Stone"], Lines(plan, Messages.Short));
    }

    [Fact]
    public void ShortMessagesStillSayWhenToPressAgain()
    {
        var plan = Plan(Inventory(), false, [Bring(Wood, 10)], Chest(1, 5, Stack(Wood, 50)));

        Assert.Contains("1 take was not made because the chest changed meanwhile; press again", Lines(plan, Messages.Short, changed: 1));
    }

    [Fact]
    public void FullMessagesGiveTheReasons()
    {
        var plan = Plan(Inventory(), false, [Bring(Wood, 10)]);

        Assert.Equal(["Workbench: took nothing", "10 Wood short: not in chests nearby"], Lines(plan, Messages.Full));
    }

    [Fact]
    public void MessagesOffSayNothingAfterAGrabOrAnAnswer()
    {
        var plan = Plan(Inventory(), false, [Bring(Wood, 10)]);

        Assert.Empty(Lines(plan, Messages.Off));
        Assert.Empty(Report.Arrivals([new Arrival(Wood, 10, 10, answered: true)], Shown, Messages.Off));
    }

    [Fact]
    public void ShortMessagesStillTellWhatArrived()
    {
        Assert.Equal(["10 Wood arrived"], Report.Arrivals([new Arrival(Wood, 10, 10, answered: true)], Shown, Messages.Short));
    }

    [Theory]
    [InlineData("Always", true, true)]
    [InlineData("Always", false, true)]
    [InlineData("UntilCarried", true, false)]
    [InlineData("UntilCarried", false, true)]
    [InlineData("Never", false, false)]
    public void TheBuildBoxShowsAsSet(string setting, bool allCarried, bool shows)
    {
        var plan = Plan(Inventory(Stack(Wood, allCarried ? 10 : 4)), false, [MakeSureICarry(Wood, 10)], Chest(1, 5, Stack(Wood, 50)));

        Assert.Equal(shows, Readout.Shows(plan, System.Enum.Parse<BuildBox>(setting)));
    }

    [Theory]
    [InlineData(true, true, true, "Search the chests, your pieces and recipes")]
    [InlineData(true, false, false, "Search the chests")]
    [InlineData(false, true, true, "Search your pieces and recipes")]
    [InlineData(true, true, false, "Search the chests and your pieces")]
    [InlineData(true, false, true, "Search the chests and your recipes")]
    [InlineData(false, true, false, "Search your pieces")]
    [InlineData(false, false, true, "Search your recipes")]
    [InlineData(false, false, false, "Nothing to search: the Search settings list nothing")]
    public void TheSearchBoxSaysWhatItSearches(bool items, bool pieces, bool recipes, string prompt)
    {
        Assert.Equal(prompt, Search.Prompt(items, pieces, recipes));
    }
}
