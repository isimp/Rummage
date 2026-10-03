using System.Linq;
using Rummage.Core;
using Xunit;

namespace Rummage.Tests;

/// <summary>Typing in the search box: what the text asks for and the order results come in.</summary>
public class SearchTests
{
    private static readonly Entry[] Catalog =
    [
        new("$item_wood", "Wood", EntryKind.Item, inChests: 120),
        new("$item_finewood", "Fine wood", EntryKind.Item, inChests: 30),
        new("$item_roundlog", "Core wood", EntryKind.Item, inChests: 0),
        new("$piece_woodwall", "Wood wall", EntryKind.Piece, inChests: 0),
        new("$item_bronzenails", "Bronze nails", EntryKind.Item, inChests: 200),
        new("$item_bronze", "Bronze", EntryKind.Item, inChests: 10),
        new("$item_snailshell", "Snail shell", EntryKind.Item, inChests: 999),
        new("$piece_workbench", "Workbench", EntryKind.Piece, inChests: 0),
        new("$item_arrow_wood", "Wood arrow", EntryKind.Recipe, inChests: 0),
    ];

    private static string[] Ranked(string text) => Search.Rank(Query.Parse(text).Text, Catalog).Select(e => e.Shown).ToArray();

    [Theory]
    [InlineData("40 wood", 40, "wood")]
    [InlineData("wood 40", 40, "wood")]
    [InlineData("3 stone portal", 3, "stone portal")]
    [InlineData("fine wood", null, "fine wood")]
    [InlineData("  wood  ", null, "wood")]
    [InlineData("12", 12, "")]
    [InlineData("", null, "")]
    public void ANumberAtEitherEndOfTheTextIsTheCount(string text, int? count, string rest)
    {
        var query = Query.Parse(text);

        Assert.Equal(count, query.Count);
        Assert.Equal(rest, query.Text);
    }

    [Fact]
    public void ACountOfZeroOrLessIsNoCount()
    {
        Assert.Null(Query.Parse("0 wood").Count);
    }

    [Fact]
    public void AnExactNameComesFirst()
    {
        Assert.Equal("Bronze", Ranked("bronze")[0]);
    }

    [Fact]
    public void NamesStartingWithTheTextComeBeforeNamesMerelyContainingIt()
    {
        var ranked = Ranked("wood");

        Assert.Equal("Wood", ranked[0]);
        Assert.True(System.Array.IndexOf(ranked, "Wood wall") < System.Array.IndexOf(ranked, "Fine wood"));
    }

    [Fact]
    public void AWordStartingWithTheTextBeatsTheTextInsideAWord()
    {
        var ranked = Search.Rank("nail", Catalog).Select(e => e.Shown).ToArray();

        Assert.Equal("Bronze nails", ranked[0]);
    }

    [Fact]
    public void LettersInOrderFindANameTooButLast()
    {
        var ranked = Ranked("wkb");

        Assert.Equal(["Workbench"], ranked);
    }

    [Fact]
    public void CaseDoesNotMatter()
    {
        Assert.Equal(Ranked("bronze nails"), Ranked("BRONZE NAILS"));
    }

    [Fact]
    public void AmongEquallyGoodMatchesWhatTheChestsHoldComesFirst()
    {
        var ranked = Ranked("wood");
        int fine = System.Array.IndexOf(ranked, "Fine wood");
        int core = System.Array.IndexOf(ranked, "Core wood");

        Assert.True(fine < core);
    }

    [Fact]
    public void WithNothingTypedTheChestsContentsAreListedByName()
    {
        var ranked = Ranked("");

        Assert.Equal(["Bronze", "Bronze nails", "Fine wood", "Snail shell", "Wood"], ranked);
    }

    [Fact]
    public void TextThatMatchesNothingListsNothing()
    {
        Assert.Empty(Ranked("zzz"));
    }
}
