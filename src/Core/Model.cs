using System;
using System.Collections.Generic;

namespace Rummage.Core;

/// <summary>A stack of items in a chest or in the inventory, as the game stacks them.</summary>
internal sealed class ItemStack
{
    public ItemStack(string name, string prefab, int count, int maxStack, int x, int y, int quality, int worldLevel, bool qualityMatters)
    {
        Name = name;
        Prefab = prefab;
        Count = count;
        MaxStack = Math.Max(1, maxStack);
        X = x;
        Y = y;
        Quality = quality;
        WorldLevel = worldLevel;
        QualityMatters = qualityMatters;
    }

    /// <summary>The item's shared name ("$item_wood"), which the game counts and builds by.</summary>
    public string Name { get; }

    /// <summary>The item's prefab name ("Wood"), which chest rules are written in.</summary>
    public string Prefab { get; }

    public int Count { get; }

    public int MaxStack { get; }

    public int X { get; }

    public int Y { get; }

    public int Quality { get; }

    public int WorldLevel { get; }

    /// <summary>Whether the item has quality levels, so that only equal qualities stack.</summary>
    public bool QualityMatters { get; }

    /// <summary>Whether the game would put the two in one slot: same item and world level, and the
    /// same quality for an item with quality levels.</summary>
    public bool StacksWith(ItemStack other)
        => other != null
            && string.Equals(Name, other.Name, StringComparison.Ordinal)
            && WorldLevel == other.WorldLevel
            && (!QualityMatters || Quality == other.Quality);
}

/// <summary>How Rummage may take from a chest.</summary>
internal enum Reach
{
    /// <summary>This game holds the chest and saves what changes in it.</summary>
    Owned,

    /// <summary>Another player's game holds the chest; a take is asked of that game, which decides.</summary>
    Routed,

    /// <summary>Nothing can save a change to the chest from here, so it is left alone.</summary>
    Closed,

    /// <summary>
    /// The chest is in reach but AzuCraftyBoxes does not let the player use it; it is only read
    /// to say why something is missing.
    /// </summary>
    Barred,
}

/// <summary>Why a chest in reach may not be used.</summary>
internal enum Bar
{
    None,

    /// <summary>Someone else has it open, or is pulling the cart it is on.</summary>
    InUse,

    /// <summary>Someone else's private chest; what it holds is not told.</summary>
    Private,

    /// <summary>Any other reason: a ward, a seal, a setting of AzuCraftyBoxes.</summary>
    Locked,
}

/// <summary>A chest within reach and what is in it.</summary>
internal sealed class Source
{
    private readonly Func<string, bool> _mayTake;

    public Source(int id, float distance, Reach reach, IReadOnlyList<ItemStack> stacks, Func<string, bool> mayTake, Bar bar = Bar.None)
    {
        Bar = bar;
        Id = id;
        Distance = distance;
        Reach = reach;
        Stacks = stacks ?? Array.Empty<ItemStack>();
        _mayTake = mayTake;
    }

    public int Id { get; }

    public float Distance { get; }

    public Reach Reach { get; }

    /// <summary>For a barred chest, why.</summary>
    public Bar Bar { get; }

    public IReadOnlyList<ItemStack> Stacks { get; }

    /// <summary>Whether the chest's rules let an item, by prefab name, be taken from it.</summary>
    public bool MayTake(string prefab) => _mayTake == null || _mayTake(prefab);
}

/// <summary>The player's inventory: its grid, what is in it, and which slots ordinary items may use.</summary>
internal sealed class Bag
{
    private readonly Func<int, int, bool> _isUsable;

    public Bag(int width, int height, IReadOnlyList<ItemStack> stacks, Func<int, int, bool> isUsable)
    {
        Width = width;
        Height = height;
        Stacks = stacks ?? Array.Empty<ItemStack>();
        _isUsable = isUsable;
    }

    public int Width { get; }

    public int Height { get; }

    public IReadOnlyList<ItemStack> Stacks { get; }

    public bool IsUsable(int x, int y) => _isUsable == null || _isUsable(x, y);
}

/// <summary>One thing a grab is for.</summary>
internal sealed class Want
{
    public Want(string name, int count, bool countCarried, int minWorldLevel)
        : this(name, count, countCarried, minWorldLevel, exact: null)
    {
    }

    private Want(string name, int count, bool countCarried, int minWorldLevel, ItemStack exact)
    {
        Name = name;
        Count = Math.Max(0, count);
        CountCarried = countCarried;
        MinWorldLevel = minWorldLevel;
        Into = exact;
    }

    /// <summary>
    /// Fills one carried stack: only items that stack with it are taken, and they go into that
    /// slot alone, whatever kind of slot it is. At most <paramref name="atMost"/> are taken when it
    /// is above zero; otherwise the stack is filled to a full one.
    /// </summary>
    public static Want TopUp(ItemStack held, int atMost = 0)
    {
        int room = held.MaxStack - held.Count;
        return new(held.Name, atMost > 0 ? Math.Min(atMost, room) : room, countCarried: false, held.WorldLevel, held);
    }

    /// <summary>For a top-up, the carried stack to fill; otherwise none.</summary>
    public ItemStack Into { get; }

    public Want WithCount(int count) => new(Name, count, CountCarried, MinWorldLevel, Into);

    public string Name { get; }

    public int Count { get; }

    /// <summary>When set, <see cref="Count"/> is a total to carry and what is carried already counts
    /// towards it; otherwise it is how many to bring.</summary>
    public bool CountCarried { get; }

    /// <summary>Items below this world level neither count nor get fetched, as building only takes
    /// items of the current world level or above.</summary>
    public int MinWorldLevel { get; }

    public bool Matches(ItemStack stack)
        => Into != null
            ? Into.StacksWith(stack)
            : string.Equals(stack.Name, Name, StringComparison.Ordinal) && stack.WorldLevel >= MinWorldLevel;
}

/// <summary>One take: an amount from a chest slot into one inventory slot that holds all of it.</summary>
internal sealed class Move
{
    public Move(int sourceId, Reach reach, string name, int fromX, int fromY, int toX, int toY, int amount)
    {
        SourceId = sourceId;
        Reach = reach;
        Name = name;
        FromX = fromX;
        FromY = fromY;
        ToX = toX;
        ToY = toY;
        Amount = amount;
    }

    public int SourceId { get; }

    public Reach Reach { get; }

    public string Name { get; }

    public int FromX { get; }

    public int FromY { get; }

    public int ToX { get; }

    public int ToY { get; }

    public int Amount { get; }
}

/// <summary>How a grab went for one item, and why anything is missing.</summary>
internal sealed class Outcome
{
    public Outcome(string name, int wanted, int carried, int need, bool total = false)
    {
        Total = total;
        Name = name;
        Wanted = wanted;
        Carried = carried;
        Need = need;
    }

    public string Name { get; }

    /// <summary>Whether this is a total to carry (a piece or recipe) rather than a number to bring.</summary>
    public bool Total { get; }

    public int Wanted { get; }

    /// <summary>What the inventory already held towards a total; zero when asked to bring a count.</summary>
    public int Carried { get; }

    /// <summary>How many the grab set out to fetch.</summary>
    public int Need { get; }

    public int Planned { get; internal set; }

    /// <summary>Units left in chests because each chest keeps one of every item it holds.</summary>
    public int KeptByLeaveOne { get; internal set; }

    /// <summary>Units in chests whose rules keep this item in.</summary>
    public int NotAllowed { get; internal set; }

    /// <summary>Units in chests nothing can save a change to from here.</summary>
    public int Unreachable { get; internal set; }

    /// <summary>Units in chests someone else has open.</summary>
    public int InUse { get; internal set; }

    /// <summary>Units in someone else's private chests, never told to the player.</summary>
    public int InPrivate { get; internal set; }

    /// <summary>Units in chests the player may not take from for any other reason.</summary>
    public int Locked { get; internal set; }

    /// <summary>Whether the inventory ran out of room before the grab was done.</summary>
    public bool OutOfRoom { get; internal set; }

    /// <summary>
    /// Whether the only slot this could go into waits for another player's chest to answer, so
    /// the rest needs another press.
    /// </summary>
    public bool AwaitsAnswer { get; internal set; }

    public int Short => Math.Max(0, Need - Planned);
}

/// <summary>How much is told after a grab.</summary>
internal enum Messages
{
    /// <summary>What was taken, and for anything short, why.</summary>
    Full,

    /// <summary>What was taken and what is short, without the reasons.</summary>
    Short,

    /// <summary>Nothing after a grab or an answer from another player's chest.</summary>
    Off,
}

/// <summary>When the box beside the game's build panel shows.</summary>
internal enum BuildBox
{
    Always,

    /// <summary>Until everything the piece needs is carried.</summary>
    UntilCarried,

    Never,
}

/// <summary>Everything one grab will do.</summary>
internal sealed class Plan
{
    public Plan(IReadOnlyList<Move> moves, IReadOnlyList<Outcome> outcomes)
    {
        Moves = moves;
        Outcomes = outcomes;
    }

    public IReadOnlyList<Move> Moves { get; }

    public IReadOnlyList<Outcome> Outcomes { get; }
}
