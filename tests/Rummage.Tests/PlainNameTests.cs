using System.Linq;
using Rummage.Core;
using Xunit;

namespace Rummage.Tests;

/// <summary>
/// Names a mod writes with rich-text markup, such as a colour, are searched, completed and shown
/// by their words alone.
/// </summary>
public class PlainNameTests
{
    [Theory]
    [InlineData("<color=#CBE86A>Boar Bait</color>", "Boar Bait")]
    [InlineData("<color=\"yellow\">Boar</color> <b>Bait</b>", "Boar Bait")]
    [InlineData("<#FF0000>Red</color> Sword", "Red Sword")]
    [InlineData("<size=120%><i>Big</i></size>  Axe ", "Big Axe")]
    [InlineData("Wood", "Wood")]
    [InlineData("Arrow <3", "Arrow <3")]
    [InlineData("", "")]
    public void MarkupIsLeftOutOfAName(string localized, string plain)
        => Assert.Equal(plain, Names.Plain(localized));

    [Fact]
    public void AColouredNameIsFoundByItsFirstWord()
    {
        Entry[] entries =
        [
            new("$item_boarbait", Names.Plain("<color=#CBE86A>Boar Bait</color>"), EntryKind.Item, inChests: 3),
            new("$item_trophy_boar", "Boar trophy", EntryKind.Item, inChests: 9),
            new("$item_raspberry", "Raspberries", EntryKind.Item, inChests: 40),
        ];

        Assert.Equal(["Boar Bait", "Boar trophy"], Search.Rank("boar", entries).Select(e => e.Shown));
    }

    [Fact]
    public void TabPutsInTheNameWithoutMarkup()
        => Assert.Equal("10 Boar Bait", Query.Complete("10 boar", Names.Plain("<color=#CBE86A>Boar Bait</color>"), ["Boar Bait"]));
}
