using System;
using System.Collections.Generic;
using System.Linq;

namespace Rummage.Core;

/// <summary>What the chests in reach can give, by the same rules a grab follows.</summary>
internal static class Stock
{
    /// <summary>How many of each item a grab could take, by item name.</summary>
    public static Dictionary<string, int> Takeable(IEnumerable<Source> sources, bool leaveOne, int minWorldLevel)
    {
        var stock = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var chest in sources)
        {
            if (chest.Reach is Reach.Closed or Reach.Barred)
            {
                continue;
            }

            foreach (var item in chest.Stacks.Where(s => s.WorldLevel >= minWorldLevel).GroupBy(s => s.Name, StringComparer.Ordinal))
            {
                int budget = Budget(chest, item.ToList(), leaveOne);
                if (budget > 0)
                {
                    stock[item.Key] = (stock.TryGetValue(item.Key, out int sum) ? sum : 0) + budget;
                }
            }
        }

        return stock;
    }

    /// <summary>
    /// What a chest gives of one item: the stacks its rules let go, less the one unit it keeps of
    /// each item it holds when Leave One Item is on.
    /// </summary>
    public static int Budget(Source chest, IReadOnlyList<ItemStack> stacksOfOneItem, bool leaveOne)
    {
        int held = stacksOfOneItem.Sum(s => s.Count);
        int allowed = stacksOfOneItem.Where(s => chest.MayTake(s.Prefab)).Sum(s => s.Count);
        return Math.Max(0, leaveOne ? Math.Min(allowed, held - 1) : allowed);
    }

    /// <summary>How many times over a cost can be paid from what is carried and what the chests give.</summary>
    public static int Affordable(IReadOnlyDictionary<string, int> cost, IReadOnlyDictionary<string, int> carried, IReadOnlyDictionary<string, int> takeable)
    {
        int times = int.MaxValue;
        foreach (var part in cost)
        {
            if (part.Value <= 0)
            {
                continue;
            }

            int have = (carried.TryGetValue(part.Key, out int c) ? c : 0) + (takeable.TryGetValue(part.Key, out int t) ? t : 0);
            times = Math.Min(times, have / part.Value);
        }

        return times == int.MaxValue ? 0 : times;
    }
}

/// <summary>For a recipe that needs any one of its ingredients: which one to fetch.</summary>
internal static class Choice
{
    /// <summary>
    /// One already carried in full first, then the first the chests can make up in full, else the
    /// one that comes closest.
    /// </summary>
    public static int Best(IReadOnlyList<Plan> plans)
    {
        for (int i = 0; i < plans.Count; i++)
        {
            if (plans[i].Outcomes.All(o => o.Need == 0))
            {
                return i;
            }
        }

        for (int i = 0; i < plans.Count; i++)
        {
            if (plans[i].Outcomes.All(o => o.Short == 0))
            {
                return i;
            }
        }

        int best = 0;
        double closest = -1;
        for (int i = 0; i < plans.Count; i++)
        {
            double share = plans[i].Outcomes.Sum(o => o.Wanted) is int wanted && wanted > 0
                ? (double)plans[i].Outcomes.Sum(o => o.Wanted - o.Short) / wanted
                : 0;
            if (share > closest)
            {
                (best, closest) = (i, share);
            }
        }

        return best;
    }
}
