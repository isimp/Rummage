using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Rummage.Core;

/// <summary>What carrying out a grab came to.</summary>
internal sealed class Done
{
    public Done(IReadOnlyDictionary<string, int> took, IReadOnlyDictionary<string, int> asked, int changed, int refused)
    {
        Took = took;
        Asked = asked;
        Changed = changed;
        Refused = refused;
    }

    /// <summary>Moved into the inventory at once, by item name.</summary>
    public IReadOnlyDictionary<string, int> Took { get; }

    /// <summary>Asked of other players' chests, whose games answer later, by item name.</summary>
    public IReadOnlyDictionary<string, int> Asked { get; }

    /// <summary>Planned takes not made because the chest no longer held what was planned.</summary>
    public int Changed { get; }

    /// <summary>Planned takes MultiUserChest turned down on the spot.</summary>
    public int Refused { get; }
}

/// <summary>What came of one take asked of another player's chest.</summary>
internal sealed class Arrival
{
    public Arrival(string name, int asked, int arrived, bool answered)
    {
        Name = name;
        Asked = asked;
        Arrived = arrived;
        Answered = answered;
    }

    public string Name { get; }

    public int Asked { get; }

    public int Arrived { get; }

    /// <summary>Whether the chest's game answered at all, rather than the wait running out.</summary>
    public bool Answered { get; }
}

/// <summary>The lines the player reads after a grab: what was taken, and why anything is missing.</summary>
internal static class Report
{
    /// <param name="label">What the grab was for.</param>
    /// <param name="plan">The grab as planned, for what was wanted and why items were missing.</param>
    /// <param name="done">What carrying it out came to.</param>
    /// <param name="shown">The item's name as the player reads it.</param>
    /// <param name="canRoute">Whether MultiUserChest is there to reach chests other players' games hold.</param>
    public static IReadOnlyList<string> Lines(string label, Plan plan, Done done, Func<string, string> shown, bool canRoute)
    {
        var lines = new List<string>();
        if (plan.Outcomes.All(o => o.Need == 0))
        {
            lines.Add($"{label}: you carry everything already");
            return lines;
        }

        string took = Items(plan, done.Took, shown);
        string asked = Items(plan, done.Asked, shown);
        lines.Add((took.Length > 0, asked.Length > 0) switch
        {
            (true, true) => $"{label}: took {took}; asked another player's chest for {asked}",
            (true, false) => $"{label}: took {took}",
            (false, true) => $"{label}: asked another player's chest for {asked}",
            _ => $"{label}: took nothing",
        });

        foreach (var outcome in plan.Outcomes.Where(o => o.Short > 0))
        {
            lines.Add($"{Count(outcome.Short, shown(outcome.Name))} short: {string.Join("; ", Reasons(outcome, canRoute))}");
        }

        if (done.Changed > 0)
        {
            lines.Add(done.Changed == 1
                ? "1 take was not made because the chest changed meanwhile; press again"
                : $"{Number(done.Changed)} takes were not made because the chests changed meanwhile; press again");
        }

        if (done.Refused > 0)
        {
            lines.Add(done.Refused == 1
                ? "1 take was turned down by another player's chest; press again"
                : $"{Number(done.Refused)} takes were turned down by another player's chest; press again");
        }

        return lines;
    }

    /// <summary>
    /// Once other players' chests have answered: what arrived, and for anything that did not,
    /// whether the chest's game turned it down or never answered.
    /// </summary>
    public static IReadOnlyList<string> Arrivals(IEnumerable<Arrival> arrivals, Func<string, string> shown)
    {
        var lines = new List<string>();
        var byItem = arrivals.GroupBy(a => a.Name, StringComparer.Ordinal).ToList();
        foreach (var item in byItem)
        {
            int arrived = item.Sum(a => Math.Min(a.Arrived, a.Asked));
            if (arrived > 0)
            {
                lines.Add($"{Count(arrived, shown(item.Key))} arrived");
            }
        }

        foreach (var item in byItem)
        {
            int turnedDown = item.Where(a => a.Answered).Sum(a => Math.Max(0, a.Asked - a.Arrived));
            int unanswered = item.Where(a => !a.Answered).Sum(a => Math.Max(0, a.Asked - a.Arrived));
            if (turnedDown > 0)
            {
                lines.Add($"{Count(turnedDown, shown(item.Key))} did not come: the other chest's game turned it down");
            }

            if (unanswered > 0)
            {
                lines.Add($"{Count(unanswered, shown(item.Key))} did not come: the other chest's game did not answer");
            }
        }

        return lines;
    }

    private static string Items(Plan plan, IReadOnlyDictionary<string, int> counts, Func<string, string> shown)
        => string.Join(", ", plan.Outcomes
            .Where(o => counts.TryGetValue(o.Name, out int n) && n > 0)
            .Select(o => Count(counts[o.Name], shown(o.Name))));

    private static IEnumerable<string> Reasons(Outcome outcome, bool canRoute)
    {
        int explained = 0;
        if (outcome.KeptByLeaveOne > 0)
        {
            explained += outcome.KeptByLeaveOne;
            yield return $"{Number(outcome.KeptByLeaveOne)} kept back by Leave One Item";
        }

        if (outcome.NotAllowed > 0)
        {
            explained += outcome.NotAllowed;
            yield return $"{Number(outcome.NotAllowed)} kept in by chest rules";
        }

        if (outcome.InUse > 0)
        {
            explained += outcome.InUse;
            yield return $"{Number(outcome.InUse)} in a chest someone has open";
        }

        if (outcome.InPrivate > 0)
        {
            explained += outcome.InPrivate;
            yield return "some in someone's private chest";
        }

        if (outcome.Locked > 0)
        {
            explained += outcome.Locked;
            yield return $"{Number(outcome.Locked)} in chests you may not take from";
        }

        if (outcome.Unreachable > 0)
        {
            explained += outcome.Unreachable;
            yield return canRoute
                ? $"{Number(outcome.Unreachable)} in a chest another player's game holds"
                : $"{Number(outcome.Unreachable)} in a chest another player's game holds, which needs MultiUserChest";
        }

        if (outcome.AwaitsAnswer)
        {
            yield return "the rest once the other chest has answered; press again";
        }
        else if (outcome.OutOfRoom)
        {
            yield return "no room left in your inventory";
        }
        else if (explained == 0)
        {
            yield return "not in chests nearby";
        }
        else if (explained < outcome.Short)
        {
            yield return "the rest is not in chests nearby";
        }
    }

    private static string Count(int count, string what) => $"{Number(count)} {what}";

    private static string Number(int count) => count.ToString("N0", CultureInfo.InvariantCulture);
}
