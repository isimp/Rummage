using Rummage.Core;
using Xunit;
using static Rummage.Tests.Fixtures;

namespace Rummage.Tests;

/// <summary>
/// The search box's list: arrows move the chosen row one at a time, Page Up and Down by the rows
/// shown, never past the ends, and the rows shown follow the chosen one so it is always in view.
/// A new search starts at the top.
/// </summary>
public class ListWindowTests
{
    private const int Shown = 5;

    [Fact]
    public void ANewSearchStartsAtTheTop()
    {
        var list = new ListWindow();
        list.Choose(7, count: 20, Shown);

        list.Restart();

        Assert.Equal(0, list.Selected);
        Assert.Equal(0, list.First);
    }

    [Fact]
    public void TheChosenRowNeverGoesPastEitherEnd()
    {
        var list = new ListWindow();

        list.Choose(-3, count: 4, Shown);
        Assert.Equal(0, list.Selected);

        list.Choose(9, count: 4, Shown);
        Assert.Equal(3, list.Selected);
    }

    [Fact]
    public void MovingBelowTheShownRowsScrollsJustEnoughToShowTheChosenOne()
    {
        var list = new ListWindow();

        list.Choose(6, count: 20, Shown);

        Assert.Equal(6, list.Selected);
        Assert.Equal(2, list.First);
    }

    [Fact]
    public void MovingAboveTheShownRowsScrollsBackToTheChosenOne()
    {
        var list = new ListWindow();
        list.Choose(12, count: 20, Shown);

        list.Choose(4, count: 20, Shown);

        Assert.Equal(4, list.First);
    }

    [Fact]
    public void MovingWithinTheShownRowsDoesNotScroll()
    {
        var list = new ListWindow();
        list.Choose(6, count: 20, Shown);

        list.Choose(3, count: 20, Shown);

        Assert.Equal(2, list.First);
    }

    [Fact]
    public void TheWheelScrollsTheRowsThreeAtATimeWithoutMovingTheChosenOne()
    {
        var list = new ListWindow();
        list.Choose(1, count: 20, Shown);

        list.Scroll(down: true, count: 20, Shown);
        Assert.Equal(3, list.First);
        Assert.Equal(1, list.Selected);

        list.Scroll(down: false, count: 20, Shown);
        Assert.Equal(0, list.First);
    }

    [Fact]
    public void TheWheelStopsWithTheLastRowsInViewAndAtTheTop()
    {
        var list = new ListWindow();

        for (int i = 0; i < 10; i++) list.Scroll(down: true, count: 20, Shown);
        Assert.Equal(15, list.First);

        for (int i = 0; i < 10; i++) list.Scroll(down: false, count: 20, Shown);
        Assert.Equal(0, list.First);
    }

    [Fact]
    public void AListThatFitsDoesNotScroll()
    {
        var list = new ListWindow();

        list.Scroll(down: true, count: 3, Shown);

        Assert.Equal(0, list.First);
    }

    [Fact]
    public void ClickingARowChoosesIt()
    {
        var list = new ListWindow();
        list.Scroll(down: true, count: 20, Shown);

        list.Click(4);

        Assert.Equal(4, list.Selected);
        Assert.Equal(3, list.First);
    }

    [Fact]
    public void AnEmptyListChoosesNothing()
    {
        var list = new ListWindow();

        list.Choose(3, count: 0, Shown);

        Assert.Equal(0, list.Selected);
        Assert.Equal(0, list.First);
    }
}

/// <summary>
/// What the search box offers and says. What the player was looking at when the box opened is
/// listed first while its name is typed. The count for an item starts at a stack, or what the
/// chests give if less, and at one for a piece or recipe; the slider goes up to what the chests
/// give, never below the start. A number typed wins over the slider. The hint line says what
/// Enter does with the chosen row now.
/// </summary>
public class SearchBoxRulesTests
{
    [Fact]
    public void WhatThePlayerWasLookingAtIsListedFirstWhileItsNameIsTyped()
    {
        Assert.True(SearchBoxRules.ContextFirst("Workbench", "Workbench"));
        Assert.True(SearchBoxRules.ContextFirst("workbench", "Workbench"));
        Assert.False(SearchBoxRules.ContextFirst("Work", "Workbench"));
        Assert.False(SearchBoxRules.ContextFirst("Workbench", null));
    }

    [Fact]
    public void AnItemsCountStartsAtAStackOrWhatTheChestsGiveIfLess()
    {
        Assert.Equal(50, SearchBoxRules.StartCount(EntryKind.Item, stackSize: 50, inChests: 120));
        Assert.Equal(12, SearchBoxRules.StartCount(EntryKind.Item, stackSize: 50, inChests: 12));
        Assert.Equal(1, SearchBoxRules.StartCount(EntryKind.Item, stackSize: 50, inChests: 0));
    }

    [Fact]
    public void APieceOrRecipeCountStartsAtOne()
    {
        Assert.Equal(1, SearchBoxRules.StartCount(EntryKind.Piece, stackSize: 20, inChests: 9));
        Assert.Equal(1, SearchBoxRules.StartCount(EntryKind.Recipe, stackSize: 20, inChests: 9));
    }

    [Fact]
    public void TheSliderGoesUpToWhatTheChestsGiveButNeverBelowTheStart()
    {
        Assert.Equal(120, SearchBoxRules.MaxCount(start: 50, inChests: 120));
        Assert.Equal(3, SearchBoxRules.MaxCount(start: 3, inChests: 2));
        Assert.Equal(1, SearchBoxRules.MaxCount(start: 1, inChests: 0));
    }

    [Fact]
    public void ATypedNumberWinsOverTheSlider()
    {
        Assert.Equal(7, SearchBoxRules.CountFor(typed: 7, slider: 30f));
        Assert.Equal(30, SearchBoxRules.CountFor(typed: null, slider: 30.2f));
        Assert.Equal(31, SearchBoxRules.CountFor(typed: null, slider: 30.7f));
    }

    [Fact]
    public void TheHintSaysWhatEnterDoesWithTheChosenRowNow()
    {
        Assert.Equal("Enter takes one of each · ↑↓ choose · Esc close", SearchBoxRules.Hint(newItems: true, takesOne: false));
        Assert.Equal("Enter takes one · Shift+Enter finds again · ↑↓ choose · Esc close", SearchBoxRules.Hint(newItems: false, takesOne: true));
        Assert.Equal("Enter find · a number, then Enter, takes · Tab complete · ↑↓ choose · Esc close", SearchBoxRules.Hint(newItems: false, takesOne: false));
    }
}

/// <summary>
/// What one craft costs, by the game's rules: only parts with an amount at that quality, an
/// upgrade station's own parts only at such a station and the ordinary parts only elsewhere, and
/// for a recipe that takes any one ingredient only those the player knows. The crafting tab makes
/// a new item at quality one, as many times as its amount says, and an upgrade at the next level,
/// once; only a recipe at quality one is remembered by name.
/// </summary>
public class RecipeRulesTests
{
    [Fact]
    public void APartWithNoAmountAtThatQualityIsLeftOut()
    {
        Assert.False(RecipeRules.Counts(amount: 0, atStation: false, stationUpgrades: false, upgraderPart: false, onlyOne: false, known: true));
        Assert.True(RecipeRules.Counts(amount: 2, atStation: false, stationUpgrades: false, upgraderPart: false, onlyOne: false, known: true));
    }

    [Fact]
    public void AnUpgradeStationsOwnPartsCountOnlyAtSuchAStation()
    {
        Assert.True(RecipeRules.Counts(amount: 1, atStation: true, stationUpgrades: true, upgraderPart: true, onlyOne: false, known: true));
        Assert.False(RecipeRules.Counts(amount: 1, atStation: true, stationUpgrades: false, upgraderPart: true, onlyOne: false, known: true));
        Assert.False(RecipeRules.Counts(amount: 1, atStation: false, stationUpgrades: false, upgraderPart: true, onlyOne: false, known: true));
    }

    [Fact]
    public void TheOrdinaryPartsCountAwayFromAnUpgradeStation()
    {
        Assert.True(RecipeRules.Counts(amount: 1, atStation: true, stationUpgrades: false, upgraderPart: false, onlyOne: false, known: true));
        Assert.False(RecipeRules.Counts(amount: 1, atStation: true, stationUpgrades: true, upgraderPart: false, onlyOne: false, known: true));
        Assert.True(RecipeRules.Counts(amount: 1, atStation: false, stationUpgrades: false, upgraderPart: false, onlyOne: false, known: true));
    }

    [Fact]
    public void ARecipeTakingAnyOneIngredientCountsOnlyThoseThePlayerKnows()
    {
        Assert.False(RecipeRules.Counts(amount: 1, atStation: false, stationUpgrades: false, upgraderPart: false, onlyOne: true, known: false));
        Assert.True(RecipeRules.Counts(amount: 1, atStation: false, stationUpgrades: false, upgraderPart: false, onlyOne: true, known: true));
        Assert.True(RecipeRules.Counts(amount: 1, atStation: false, stationUpgrades: false, upgraderPart: false, onlyOne: false, known: false));
    }

    [Fact]
    public void ANewItemIsMadeAtQualityOneAsOftenAsTheCraftingTabSays()
    {
        Assert.Equal((1, 5), RecipeRules.Craft(upgrading: null, craftAmount: 5));
    }

    [Fact]
    public void AnUpgradeIsMadeAtTheNextLevelOnce()
    {
        Assert.Equal((3, 1), RecipeRules.Craft(upgrading: 2, craftAmount: 5));
    }

    [Fact]
    public void OnlyARecipeAtQualityOneIsRememberedByName()
    {
        Assert.Equal("$item_sword_iron", RecipeRules.KeyOf(1, Sword));
        Assert.Null(RecipeRules.KeyOf(2, Sword));
    }
}

/// <summary>
/// How a grab is named in its lines: "Wood x20" for more than one, "Wood" for one, and "Wood to
/// a full stack" when it tops a carried stack up to full. A grab is always for at least one.
/// </summary>
public class LabelTests
{
    [Fact]
    public void MoreThanOneIsCounted()
    {
        Assert.Equal("Wood x20", Labels.Of("Wood", 20, into: null));
        Assert.Equal("Wood", Labels.Of("Wood", 1, into: null));
    }

    [Fact]
    public void ATopUpToAFullStackSaysSo()
    {
        var held = Stack(Wood, 30, maxStack: 50);

        Assert.True(Labels.FillsStack(20, held));
        Assert.Equal("Wood to a full stack", Labels.Of("Wood", 20, held));
        Assert.False(Labels.FillsStack(19, held));
        Assert.Equal("Wood x19", Labels.Of("Wood", 19, held));
    }

    [Fact]
    public void AGrabIsForAtLeastOne()
    {
        Assert.Equal(1, Labels.Count(0));
        Assert.Equal(1, Labels.Count(-4));
        Assert.Equal(6, Labels.Count(6));
    }
}
