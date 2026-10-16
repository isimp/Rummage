using System.Collections.Generic;
using System.Linq;
using Rummage.Core;
using Xunit;
using static Rummage.Tests.Fixtures;

namespace Rummage.Tests;

/// <summary>
/// The search box remembers the last two things it found or took, per character, and lists them
/// first with nothing typed. Something it found stays found until it is taken or something else
/// is found; Enter on it then takes one instead of finding it again.
/// </summary>
public class LastUsedTests
{
    private const string Portal = "$piece_portal";
    private const string Resin = "$item_resin";

    [Fact]
    public void EnterWithoutACountFindsWhatWasNotFoundYet()
    {
        var recent = new Recent();

        Assert.Equal(EnterDoes.Find, recent.Enter(EntryKind.Item, Wood, countTyped: false, shift: false));
    }

    [Fact]
    public void EnterAgainOnWhatWasFoundTakesOne()
    {
        var recent = new Recent();
        recent.Found(EntryKind.Item, Wood);

        Assert.Equal(EnterDoes.TakeOne, recent.Enter(EntryKind.Item, Wood, countTyped: false, shift: false));
    }

    [Fact]
    public void ShiftEnterFindsAgainWhatWasFound()
    {
        var recent = new Recent();
        recent.Found(EntryKind.Item, Wood);

        Assert.Equal(EnterDoes.Find, recent.Enter(EntryKind.Item, Wood, countTyped: false, shift: true));
    }

    [Fact]
    public void ACountTypedAlwaysTakesThatMany()
    {
        var recent = new Recent();
        recent.Found(EntryKind.Item, Wood);

        Assert.Equal(EnterDoes.Take, recent.Enter(EntryKind.Item, Wood, countTyped: true, shift: false));
        Assert.Equal(EnterDoes.Take, recent.Enter(EntryKind.Item, Stone, countTyped: true, shift: true));
    }

    [Fact]
    public void TheNewItemsRowAlwaysTakes()
    {
        Assert.Equal(EnterDoes.Take, new Recent().Enter(EntryKind.New, NewItems.Key, countTyped: false, shift: false));
    }

    [Fact]
    public void TakingWhatWasFoundEndsItsFind()
    {
        var recent = new Recent();
        recent.Found(EntryKind.Item, Wood);
        recent.Took(EntryKind.Item, Wood);

        Assert.Equal(EnterDoes.Find, recent.Enter(EntryKind.Item, Wood, countTyped: false, shift: false));
    }

    [Fact]
    public void FindingSomethingElseEndsTheFirstFind()
    {
        var recent = new Recent();
        recent.Found(EntryKind.Item, Wood);
        recent.Found(EntryKind.Item, Stone);

        Assert.Equal(EnterDoes.Find, recent.Enter(EntryKind.Item, Wood, countTyped: false, shift: false));
        Assert.Equal(EnterDoes.TakeOne, recent.Enter(EntryKind.Item, Stone, countTyped: false, shift: false));
    }

    [Fact]
    public void TakingSomethingElseKeepsTheFind()
    {
        var recent = new Recent();
        recent.Found(EntryKind.Item, Wood);
        recent.Took(EntryKind.Piece, Portal);

        Assert.Equal(EnterDoes.TakeOne, recent.Enter(EntryKind.Item, Wood, countTyped: false, shift: false));
    }

    [Fact]
    public void AnItemAndARecipeOfTheSameNameAreApart()
    {
        var recent = new Recent();
        recent.Found(EntryKind.Recipe, Wood);

        Assert.Equal(EnterDoes.Find, recent.Enter(EntryKind.Item, Wood, countTyped: false, shift: false));
    }

    [Fact]
    public void OnlyTheLastTwoAreKeptNewestFirst()
    {
        var recent = new Recent();
        recent.Took(EntryKind.Item, Wood);
        recent.Found(EntryKind.Item, Stone);
        recent.Took(EntryKind.Piece, Portal);

        Assert.Equal([(EntryKind.Piece, Portal), (EntryKind.Item, Stone)], recent.Entries.Select(u => (u.Kind, u.Key)));
    }

    [Fact]
    public void UsingSomethingAgainMovesItToTheFrontOnce()
    {
        var recent = new Recent();
        recent.Took(EntryKind.Item, Wood);
        recent.Took(EntryKind.Item, Stone);
        recent.Found(EntryKind.Item, Wood);

        Assert.Equal([(EntryKind.Item, Wood), (EntryKind.Item, Stone)], recent.Entries.Select(u => (u.Kind, u.Key)));
    }

    [Fact]
    public void UsingTheSameThingTwiceKeepsRoomForAnother()
    {
        var recent = new Recent();
        recent.Took(EntryKind.Item, Stone);
        recent.Found(EntryKind.Item, Wood);
        recent.Took(EntryKind.Item, Wood);

        Assert.Equal([(EntryKind.Item, Wood), (EntryKind.Item, Stone)], recent.Entries.Select(u => (u.Kind, u.Key)));
    }

    [Fact]
    public void TheNewItemsRowIsNotRemembered()
    {
        var recent = new Recent();
        recent.Took(EntryKind.New, NewItems.Key);

        Assert.Empty(recent.Entries);
    }

    [Fact]
    public void EachRowSaysWhetherItWasFoundOrTaken()
    {
        var recent = new Recent();
        recent.Took(EntryKind.Item, Wood);
        recent.Found(EntryKind.Item, Stone);

        Assert.Equal("found", recent.Tag(EntryKind.Item, Stone));
        Assert.Equal("taken", recent.Tag(EntryKind.Item, Wood));
        Assert.Null(recent.Tag(EntryKind.Piece, Portal));
    }

    [Fact]
    public void SomethingFoundBeforeSomethingElseWasFoundIsNotMarkedFound()
    {
        var recent = new Recent();
        recent.Found(EntryKind.Item, Wood);
        recent.Found(EntryKind.Item, Stone);

        Assert.Null(recent.Tag(EntryKind.Item, Wood));
    }

    [Fact]
    public void WhatIsRememberedIsThereAfterARestart()
    {
        var recent = new Recent();
        recent.Took(EntryKind.Piece, Portal);
        recent.Found(EntryKind.Item, Wood);

        var loaded = Recent.Load(recent.Save());

        Assert.Equal(recent.Entries.Select(u => (u.Kind, u.Key)), loaded.Entries.Select(u => (u.Kind, u.Key)));
        Assert.Equal(EnterDoes.TakeOne, loaded.Enter(EntryKind.Item, Wood, countTyped: false, shift: false));
        Assert.Equal("taken", loaded.Tag(EntryKind.Piece, Portal));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a line we wrote\n\t\t\nItem")]
    public void AFileThatCannotBeReadStartsEmpty(string text)
    {
        Assert.Empty(Recent.Load(text).Entries);
    }

    [Fact]
    public void ALineThatCannotBeReadIsSkippedAndTheRestKept()
    {
        var loaded = Recent.Load("garbage\nItem\ttaken\t$item_wood\n");

        Assert.Equal([(EntryKind.Item, Wood)], loaded.Entries.Select(u => (u.Kind, u.Key)));
    }

    [Fact]
    public void ForgettingClearsEverything()
    {
        var recent = new Recent();
        recent.Found(EntryKind.Item, Wood);
        recent.Forget();

        Assert.Empty(recent.Entries);
        Assert.Equal(EnterDoes.Find, recent.Enter(EntryKind.Item, Wood, countTyped: false, shift: false));
    }

    [Fact]
    public void EveryChangeIsCountedSoItCanBeSaved()
    {
        var recent = new Recent();
        int before = recent.Revision;
        recent.Found(EntryKind.Item, Wood);

        Assert.NotEqual(before, recent.Revision);
    }

    private static Entry Item(string key, int held) => new(key, key.Replace("$item_", ""), EntryKind.Item, held, held);

    private static readonly Entry NewRow = new(NewItems.Key, NewItems.Label, EntryKind.New, 3, 3);

    private static readonly Entry PortalRow = new(Portal, "Portal", EntryKind.Piece, 1);

    [Fact]
    public void WithNothingTypedTheLastUsedFollowTheNewItemsRow()
    {
        var entries = new List<Entry> { Item(Wood, 98), Item(Stone, 12), Item(Nails, 5), PortalRow, NewRow };
        var recent = new Recent();
        recent.Took(EntryKind.Item, Stone);
        recent.Found(EntryKind.Piece, Portal);

        var ranked = Search.Rank(string.Empty, entries, recent.Entries);

        Assert.Equal([NewItems.Key, Portal, Stone, Nails, Wood], ranked.Select(e => e.Key));
    }

    [Fact]
    public void ALastUsedItemNoLongerInTheChestsStillShows()
    {
        var entries = new List<Entry> { Item(Wood, 98), Item(Resin, 0) };
        var recent = new Recent();
        recent.Took(EntryKind.Item, Resin);

        Assert.Equal([Resin, Wood], Search.Rank(string.Empty, entries, recent.Entries).Select(e => e.Key));
        Assert.Equal([Wood], Search.Rank(string.Empty, entries, new Recent().Entries).Select(e => e.Key));
    }

    [Fact]
    public void ALastUsedEntryTheSearchBoxNoLongerListsIsLeftOut()
    {
        var entries = new List<Entry> { Item(Wood, 98) };
        var recent = new Recent();
        recent.Took(EntryKind.Piece, Portal);

        Assert.Equal([Wood], Search.Rank(string.Empty, entries, recent.Entries).Select(e => e.Key));
    }

    [Fact]
    public void WithTextTypedTheOrderIsTheUsualOne()
    {
        var entries = new List<Entry> { Item(Wood, 98), Item("$item_woodarrow", 5) };
        var recent = new Recent();
        recent.Took(EntryKind.Item, "$item_woodarrow");

        Assert.Equal([Wood, "$item_woodarrow"], Search.Rank("wood", entries, recent.Entries).Select(e => e.Key));
    }
}
