using Rummage.Core;
using Xunit;

namespace Rummage.Tests;

/// <summary>
/// The search text: a number at either end is the count, unless the words typed are the start of
/// a name, numbers and all; and Tab completes a name while keeping the count in front.
/// </summary>
public class QueryTests
{
    private static readonly string[] Names = ["Wood", "Wood 2x2 floor", "Banner 2", "Banner 10", "Bronze nails"];

    [Theory]
    [InlineData("40 wood", 40, "wood")]
    [InlineData("wood 40", 40, "wood")]
    [InlineData("Banner 2", null, "Banner 2")]
    [InlineData("banner 2", null, "banner 2")]
    [InlineData("1 Banner 2", 1, "Banner 2")]
    [InlineData("Banner 2 3", 3, "Banner 2")]
    [InlineData("wood 2", 2, "wood")]
    [InlineData("banner 1", 1, "banner")]
    [InlineData("banner 10", null, "banner 10")]
    public void ANumberThatIsPartOfANameStaysInTheName(string text, int? count, string rest)
    {
        var query = Query.Parse(text, Names);

        Assert.Equal(count, query.Count);
        Assert.Equal(rest, query.Text);
    }

    [Theory]
    [InlineData("40 wo", "Wood", "40 Wood")]
    [InlineData("wo 40", "Wood", "40 Wood")]
    [InlineData("wo", "Wood", "Wood")]
    [InlineData("", "Wood", "Wood")]
    [InlineData("40", "Bronze nails", "40 Bronze nails")]
    [InlineData("3 ban", "Banner 2", "3 Banner 2")]
    [InlineData("ban", "Banner 2", "Banner 2")]
    public void TabCompletesTheNameAndKeepsTheCountInFront(string text, string chosen, string completed)
    {
        Assert.Equal(completed, Query.Complete(text, chosen, Names));
    }

    [Fact]
    public void ACompletedNameWithANumberIsReadAsTheNameAgain()
    {
        string completed = Query.Complete("ban", "Banner 2", Names);
        var query = Query.Parse(completed, Names);

        Assert.Null(query.Count);
        Assert.Equal("Banner 2", query.Text);
    }

    [Fact]
    public void ACompletedNameWithANumberKeepsItsCount()
    {
        var query = Query.Parse(Query.Complete("3 ban", "Banner 2", Names), Names);

        Assert.Equal(3, query.Count);
        Assert.Equal("Banner 2", query.Text);
    }
}
